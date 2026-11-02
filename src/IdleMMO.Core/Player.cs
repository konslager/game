using System.Text.Json.Serialization;

namespace IdleMMO.Core;

public sealed class Player
{
    public const int InventoryCapacity = 40;

    public string Name { get; set; } = "";
    public string ClassId { get; set; } = ClassCatalog.Gladiator.Id;
    public int Level { get; set; } = 1;
    public long Exp { get; set; }
    public long Gold { get; set; }
    public int CurrentHp { get; set; }
    public int CurrentMana { get; set; }
    public int Shield { get; set; }
    public int HighestZoneUnlocked { get; set; } = 1;
    public List<Item> Inventory { get; set; } = new();
    public Dictionary<EquipSlot, Item> Equipment { get; set; } = new();

    /// <summary>Remaining cooldown per skill id (runtime state, not persisted).</summary>
    [JsonIgnore] public Dictionary<string, int> Cooldowns { get; } = new();

    /// <summary>Fractional attack accumulator driven by AttackSpeed.</summary>
    [JsonIgnore] public double ActionMeter { get; set; }
    private double _regenCarry;

    public Player() { }

    public Player(string name, string classId)
    {
        Name = name;
        ClassId = classId;
        FullRestore();
    }

    [JsonIgnore] public ClassDef Class => ClassCatalog.Get(ClassId);

    [JsonIgnore]
    public Attributes Attributes
    {
        get
        {
            var a = Class.BaseAttributes.Clone();
            for (int i = 1; i < Level; i++) a.Add(Class.GrowthPerLevel);
            foreach (var item in Equipment.Values) a.Add(item.EffectiveBonus);
            return a;
        }
    }

    [JsonIgnore] public DerivedStats Stats => DerivedStats.From(Attributes, Equipment.Values.Sum(i => i.EffectiveDefense));

    [JsonIgnore] public bool IsDead => CurrentHp <= 0;

    public static long ExpToNext(int level) => (long)Math.Round(100 * Math.Pow(level, 2.2));

    [JsonIgnore] public IEnumerable<SkillDef> UnlockedSkills => Class.Skills.Where(s => s.UnlockLevel <= Level);

    public bool IsReady(SkillDef s) =>
        s.UnlockLevel <= Level && CurrentMana >= s.ManaCost && Cooldowns.GetValueOrDefault(s.Id) <= 0;

    public void FullRestore()
    {
        var st = Stats;
        CurrentHp = st.MaxHp;
        CurrentMana = st.MaxMana;
        Shield = 0;
        Cooldowns.Clear();
        ActionMeter = 0;
    }

    /// <summary>Returns number of levels gained.</summary>
    public int GainExp(long amount)
    {
        Exp += amount;
        int gained = 0;
        while (Exp >= ExpToNext(Level))
        {
            Exp -= ExpToNext(Level);
            Level++;
            gained++;
        }
        if (gained > 0) FullRestore();
        return gained;
    }

    public void GainGold(long amount) => Gold += amount;

    public int TakeDamage(int raw)
    {
        int dmg = DerivedStats.Mitigate(raw, Stats.Defense);
        int absorbed = Math.Min(Shield, dmg);
        Shield -= absorbed;
        dmg -= absorbed;
        CurrentHp = Math.Max(0, CurrentHp - dmg);
        return dmg;
    }

    public void Heal(int amount) => CurrentHp = Math.Min(Stats.MaxHp, CurrentHp + amount);

    /// <summary>Per-tick upkeep: regen and cooldown countdown.</summary>
    public void Tick()
    {
        var st = Stats;
        _regenCarry += st.HpRegen;
        int whole = (int)_regenCarry;
        _regenCarry -= whole;
        Heal(whole);
        CurrentMana = Math.Min(st.MaxMana, CurrentMana + (int)Math.Ceiling(st.ManaRegen));
        foreach (var key in Cooldowns.Keys.ToList())
            if (Cooldowns[key] > 0) Cooldowns[key]--;
    }

    // ---------- Gear ----------

    /// <summary>Adds loot: auto-equips if stronger, otherwise stores; salvages when inventory is full.</summary>
    public LootOutcome ReceiveItem(Item item)
    {
        if (!Equipment.TryGetValue(item.Slot, out var current) || item.PowerScore > current.PowerScore)
        {
            Equip(item);
            return LootOutcome.Equipped;
        }
        if (Inventory.Count >= InventoryCapacity)
        {
            Gold += item.SalvageValue;
            return LootOutcome.Salvaged;
        }
        Inventory.Add(item);
        return LootOutcome.Stored;
    }

    public void Equip(Item item)
    {
        Inventory.Remove(item);
        if (Equipment.TryGetValue(item.Slot, out var old))
        {
            if (Inventory.Count < InventoryCapacity) Inventory.Add(old);
            else Gold += old.SalvageValue;
        }
        int oldMax = Stats.MaxHp;
        Equipment[item.Slot] = item;
        int newMax = Stats.MaxHp;
        CurrentHp = Math.Clamp(CurrentHp + (newMax - oldMax), 1, newMax);
    }

    public long SalvageInventory()
    {
        long total = Inventory.Sum(i => i.SalvageValue);
        Inventory.Clear();
        Gold += total;
        return total;
    }

    public bool TryEnhance(EquipSlot slot)
    {
        if (!Equipment.TryGetValue(slot, out var item) || !item.CanEnhance || Gold < item.EnhanceCost) return false;
        Gold -= item.EnhanceCost;
        item.EnhancementLevel++;
        return true;
    }

    /// <summary>Tier promotion (e.g. Uncommon → Rare). Requires +5; resets enhancement to 0.</summary>
    public bool TryPromote(EquipSlot slot)
    {
        if (!Equipment.TryGetValue(slot, out var item) || !item.CanPromote || Gold < item.PromoteCost) return false;
        Gold -= item.PromoteCost;
        item.Tier++;
        item.EnhancementLevel = 0;
        return true;
    }
}

public enum LootOutcome { Equipped, Stored, Salvaged }
