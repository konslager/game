namespace IdleMMO.Core;

public enum SessionMode { Farming, BossFight }

/// <summary>
/// Core loop: farm → loot/exp → upgrade → boss gate → next zone (win) or re-farm (loss).
/// One call to <see cref="GameTick"/> = one second of game time.
/// </summary>
public sealed class GameSession
{
    public const int LogCapacity = 30;

    public Player Player { get; private set; }
    public Zone CurrentZone { get; private set; }
    public Monster? ActiveMonster { get; private set; }
    public SessionMode Mode { get; private set; } = SessionMode.Farming;
    public int KillsInZone { get; set; }
    public bool AutoBoss { get; set; } = true;
    public bool AutoAdvance { get; set; } = true;
    public bool LootEnabled { get; set; } = true;
    public long TotalTicks { get; private set; }

    private readonly Random _rng;
    private readonly LinkedList<string> _log = new();
    public IEnumerable<string> Log => _log;
    public event Action<string>? Message;

    public GameSession(Player player, int zoneId = 1, int? seed = null)
    {
        Player = player;
        CurrentZone = ZoneCatalog.Get(zoneId);
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public static GameSession NewGame(string name, string classId = "gladiator", int? seed = null) =>
        new(new Player(name, classId), 1, seed);

    public int KillsNeededForBoss => Math.Max(0, CurrentZone.KillsForBoss - KillsInZone);
    public bool BossAvailable => KillsNeededForBoss == 0 && Mode == SessionMode.Farming;

    public void GameTick()
    {
        TotalTicks++;
        Player.Tick();

        if (ActiveMonster is null || ActiveMonster.IsDead)
        {
            if (AutoBoss && BossAvailable && Player.Level >= CurrentZone.RequiredLevel) StartBoss();
            else ActiveMonster = CurrentZone.SpawnMonster(_rng);
        }

        var target = ActiveMonster!;
        Player.ActionMeter += Player.Stats.AttackSpeed;
        while (Player.ActionMeter >= 1 && !target.IsDead)
        {
            Player.ActionMeter -= 1;
            PlayerAct(target);
        }

        if (target.IsDead) { OnMonsterKilled(target); return; }

        Player.TakeDamage(target.Damage);
        if (Player.IsDead) OnPlayerDefeated();
    }

    public void RunTicks(int n) { for (int i = 0; i < n; i++) GameTick(); }

    private void PlayerAct(Monster target)
    {
        var skill = CombatAI.Decide(Player);
        var st = Player.Stats;

        if (skill != null)
        {
            Player.CurrentMana -= skill.ManaCost;
            Player.Cooldowns[skill.Id] = skill.CooldownTicks;
        }

        switch (skill?.Kind)
        {
            case SkillKind.Heal:
                Player.Heal((int)(st.MaxHp * skill.Power));
                return;
            case SkillKind.Shield:
                Player.Shield = (int)(st.MaxHp * skill.Power);
                return;
        }

        double raw = st.PhysicalDamage * (skill?.Power ?? 1.0);
        if (_rng.NextDouble() < st.CritChance) raw *= st.CritDamage;
        target.TakeDamage((int)raw);
    }

    private void OnMonsterKilled(Monster m)
    {
        int levels = Player.GainExp(m.RewardExp);
        Player.GainGold(m.RewardGold);
        if (levels > 0) Emit($"Level up! Now level {Player.Level}.");

        if (LootEnabled && (m.IsBoss || _rng.NextDouble() < CurrentZone.LootChance))
        {
            var item = LootTable.Generate(CurrentZone, _rng, m.IsBoss);
            var outcome = Player.ReceiveItem(item);
            Emit($"Loot: {item} → {outcome}");
        }

        if (m.IsBoss)
        {
            Mode = SessionMode.Farming;
            Emit($"Boss defeated: {m.Name}!");
            int next = CurrentZone.Id + 1;
            if (next <= ZoneCatalog.Count)
            {
                Player.HighestZoneUnlocked = Math.Max(Player.HighestZoneUnlocked, next);
                if (AutoAdvance && Player.Level >= ZoneCatalog.Get(next).RequiredLevel) TravelTo(next);
            }
            KillsInZone = 0;
        }
        else KillsInZone++;

        ActiveMonster = null;
    }

    private void OnPlayerDefeated()
    {
        Emit(ActiveMonster?.IsBoss == true
            ? $"Defeated by {ActiveMonster.Name}. Re-farming before the next attempt."
            : "You were defeated. Respawning.");
        if (Mode == SessionMode.BossFight)
        {
            Mode = SessionMode.Farming;
            KillsInZone = CurrentZone.KillsForBoss / 2; // must re-farm half the gate before retrying
        }
        Player.FullRestore();
        ActiveMonster = null;
    }

    public bool StartBoss()
    {
        if (!BossAvailable || Player.Level < CurrentZone.RequiredLevel) return false;
        Mode = SessionMode.BossFight;
        Player.FullRestore();
        ActiveMonster = CurrentZone.SpawnBoss();
        Emit($"Boss challenge: {ActiveMonster.Name} ({ActiveMonster.MaxHp} HP)");
        return true;
    }

    public bool TravelTo(int zoneId)
    {
        if (zoneId < 1 || zoneId > ZoneCatalog.Count || zoneId > Player.HighestZoneUnlocked) return false;
        var zone = ZoneCatalog.Get(zoneId);
        if (Player.Level < zone.RequiredLevel) return false;
        CurrentZone = zone;
        Mode = SessionMode.Farming;
        KillsInZone = 0;
        ActiveMonster = null;
        Emit($"Entered zone {zone.Id}: {zone.Name}");
        return true;
    }

    /// <summary>Equips the best stored items, salvages obsolete ones, then enhances/promotes gear while gold allows.</summary>
    public int AutoUpgrade()
    {
        int actions = 0;
        foreach (var best in Player.Inventory.GroupBy(i => i.Slot).Select(g => g.MaxBy(i => i.PowerScore)!).ToList())
        {
            if (!Player.Equipment.TryGetValue(best.Slot, out var cur) || best.PowerScore > cur.PowerScore)
            { Player.Equip(best); actions++; }
        }

        var obsolete = Player.Inventory
            .Where(i => Player.Equipment.TryGetValue(i.Slot, out var eq) && i.PowerScore <= eq.PowerScore).ToList();
        foreach (var junk in obsolete) { Player.Inventory.Remove(junk); Player.Gold += junk.SalvageValue; }

        bool progress = true;
        while (progress)
        {
            progress = false;
            // Cheapest upgrade first spreads gold across slots.
            foreach (var item in Player.Equipment.Values.OrderBy(i => i.CanPromote ? i.PromoteCost : i.EnhanceCost).ToList())
            {
                if (item.CanPromote && Player.TryPromote(item.Slot)) { actions++; progress = true; break; }
                if (item.EnhancementLevel < Item.PromoteAtEnhancement || item.Tier == ItemTier.Legendary)
                    if (Player.TryEnhance(item.Slot)) { actions++; progress = true; break; }
            }
        }
        return actions;
    }

    private void Emit(string msg)
    {
        _log.AddLast(msg);
        while (_log.Count > LogCapacity) _log.RemoveFirst();
        Message?.Invoke(msg);
    }
}
