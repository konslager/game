using IdleMMO.Core;
using Xunit;

namespace IdleMMO.Tests;

public class CoreTests
{
    private static Player NewPlayer() => new("T", "gladiator");

    [Fact]
    public void Ai_HealsBelow30Percent()
    {
        var p = NewPlayer();
        p.CurrentHp = (int)(p.Stats.MaxHp * 0.2);
        Assert.Equal(SkillKind.Heal, CombatAI.Decide(p)!.Kind);
    }

    [Fact]
    public void Ai_UsesStrongestReadyDamageSkill_ThenFallsBackToAutoAttack()
    {
        var p = NewPlayer();
        p.Level = 5;
        p.FullRestore();
        Assert.Equal("ruin", CombatAI.Decide(p)!.Id);

        foreach (var s in p.Class.Skills) p.Cooldowns[s.Id] = 5;
        Assert.Null(CombatAI.Decide(p));
    }

    [Fact]
    public void GainExp_LevelsUpAndCarriesRemainder()
    {
        var p = NewPlayer();
        long need = Player.ExpToNext(1);
        int levels = p.GainExp(need + 10);
        Assert.Equal(1, levels);
        Assert.Equal(2, p.Level);
        Assert.Equal(10, p.Exp);
    }

    [Fact]
    public void ReceiveItem_AutoEquipsBetterAndStoresWorse()
    {
        var p = NewPlayer();
        var weak = new Item("W", EquipSlot.Weapon, ItemTier.Common, 1, new Attributes(5, 0, 0, 0, 0));
        var strong = new Item("S", EquipSlot.Weapon, ItemTier.Rare, 1, new Attributes(5, 0, 0, 0, 0));
        int baseStr = p.Attributes.Strength;

        Assert.Equal(LootOutcome.Equipped, p.ReceiveItem(weak));
        Assert.Equal(LootOutcome.Equipped, p.ReceiveItem(strong));
        Assert.Same(strong, p.Equipment[EquipSlot.Weapon]);
        Assert.Contains(weak, p.Inventory);
        Assert.Equal(baseStr + strong.EffectiveBonus.Strength, p.Attributes.Strength);

        var worse = new Item("X", EquipSlot.Weapon, ItemTier.Common, 1, new Attributes(1, 0, 0, 0, 0));
        Assert.Equal(LootOutcome.Stored, p.ReceiveItem(worse));
    }

    [Fact]
    public void Enhance_ThenPromote_MovesTierUpAndResetsEnhancement()
    {
        var p = NewPlayer();
        var item = new Item("G", EquipSlot.Weapon, ItemTier.Uncommon, 1, new Attributes(10, 0, 0, 0, 0));
        p.Equip(item);
        p.Gold = 1_000_000;

        Assert.False(p.TryPromote(EquipSlot.Weapon));
        for (int i = 0; i < Item.PromoteAtEnhancement; i++) Assert.True(p.TryEnhance(EquipSlot.Weapon));
        Assert.True(p.TryPromote(EquipSlot.Weapon));
        Assert.Equal(ItemTier.Rare, item.Tier);
        Assert.Equal(0, item.EnhancementLevel);
        Assert.True(p.Gold < 1_000_000);
    }

    [Fact]
    public void Session_FarmsKillsAndGainsRewards()
    {
        var s = GameSession.NewGame("T", seed: 42);
        s.AutoBoss = false;
        s.RunTicks(200);
        Assert.True(s.KillsInZone > 10);
        Assert.True(s.Player.Gold > 0);
        Assert.True(s.Player.Level > 1 || s.Player.Exp > 0);
    }

    [Fact]
    public void BossWin_UnlocksAndAdvancesToNextZone()
    {
        var s = GameSession.NewGame("T", seed: 1);
        s.RunTicks(3000);
        Assert.True(s.Player.HighestZoneUnlocked >= 2);
        Assert.True(s.CurrentZone.Id >= 2);
    }

    [Fact]
    public void BossLoss_ReturnsToFarmingWithRefarmPenalty()
    {
        var s = GameSession.NewGame("T", seed: 1);
        s.KillsInZone = s.CurrentZone.KillsForBoss;
        s.AutoBoss = false;
        s.Player.CurrentHp = 1;
        Assert.True(s.StartBoss());      // StartBoss restores HP, so cripple afterwards
        s.Player.CurrentHp = 1;
        s.Player.Shield = 0;
        foreach (var sk in s.Player.Class.Skills) s.Player.Cooldowns[sk.Id] = 99;

        s.GameTick();

        Assert.Equal(SessionMode.Farming, s.Mode);
        Assert.Equal(s.CurrentZone.KillsForBoss / 2, s.KillsInZone);
        Assert.Equal(1, s.CurrentZone.Id);
        Assert.Equal(s.Player.Stats.MaxHp, s.Player.CurrentHp);
    }

    [Fact]
    public void TravelTo_RespectsUnlockAndLevelGates()
    {
        var s = GameSession.NewGame("T", seed: 1);
        Assert.False(s.TravelTo(2));                 // locked
        s.Player.HighestZoneUnlocked = 2;
        Assert.False(s.TravelTo(2));                 // level too low
        s.Player.Level = ZoneCatalog.Get(2).RequiredLevel;
        Assert.True(s.TravelTo(2));
    }

    [Fact]
    public void SaveLoad_RoundTripsState()
    {
        var s = GameSession.NewGame("Saver", seed: 7);
        s.RunTicks(1500);
        s.AutoUpgrade();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var json = SaveSystem.Serialize(s, now);
        var (loaded, savedAt) = SaveSystem.Deserialize(json);

        Assert.Equal(now, savedAt);
        Assert.Equal(s.Player.Level, loaded.Player.Level);
        Assert.Equal(s.Player.Gold, loaded.Player.Gold);
        Assert.Equal(s.Player.Exp, loaded.Player.Exp);
        Assert.Equal(s.CurrentZone.Id, loaded.CurrentZone.Id);
        Assert.Equal(s.Player.Equipment.Count, loaded.Player.Equipment.Count);
        Assert.Equal(s.Player.Stats, loaded.Player.Stats);
        Assert.Contains("\"Weapon\"", json); // enums as strings
    }

    [Fact]
    public void SaveToDisk_IsAtomicAndLoadable()
    {
        var dir = Path.Combine(Path.GetTempPath(), "idlemmo-" + Guid.NewGuid());
        var path = Path.Combine(dir, "save.json");
        try
        {
            var s = GameSession.NewGame("Disk", seed: 3);
            SaveSystem.Save(s, path, DateTime.UtcNow);
            Assert.False(File.Exists(path + ".tmp"));
            Assert.NotNull(SaveSystem.Load(path));
            Assert.Null(SaveSystem.Load(Path.Combine(dir, "missing.json")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OfflineProgress_GrantsRewardsAndIsCapped()
    {
        var a = GameSession.NewGame("A", seed: 5);
        var r1 = OfflineProgress.Apply(a, TimeSpan.FromHours(1));
        Assert.True(r1.ExpGained > 0 && r1.GoldGained > 0);
        Assert.Equal(TimeSpan.FromHours(1), r1.Credited);

        var b = GameSession.NewGame("B", seed: 5);
        var r2 = OfflineProgress.Apply(b, TimeSpan.FromDays(3));
        Assert.Equal(OfflineProgress.MaxOffline, r2.Credited);

        var c = GameSession.NewGame("C", seed: 5);
        Assert.Equal(0, OfflineProgress.Apply(c, TimeSpan.FromSeconds(-5)).ExpGained);
    }

    [Fact]
    public void AutoUpgrade_SalvagesObsoleteInventory()
    {
        var s = GameSession.NewGame("T", seed: 1);
        var p = s.Player;
        p.Equip(new Item("Good", EquipSlot.Ring, ItemTier.Epic, 5, new Attributes(10, 10, 10, 0, 10)));
        p.Inventory.Add(new Item("Junk", EquipSlot.Ring, ItemTier.Common, 1, new Attributes(1, 0, 0, 0, 0)));
        s.AutoUpgrade();
        Assert.Empty(p.Inventory);
    }

    [Fact]
    public void Mitigation_NeverBelowOne()
    {
        Assert.Equal(1, DerivedStats.Mitigate(1, 100_000));
        Assert.Equal(50, DerivedStats.Mitigate(100, 100));
    }
}
