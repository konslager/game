namespace IdleMMO.Core;

public sealed class Monster
{
    public string Name { get; }
    public int MaxHp { get; }
    public int CurrentHp { get; private set; }
    public int Damage { get; }
    public int Defense { get; }
    public long RewardExp { get; }
    public long RewardGold { get; }
    public bool IsBoss { get; }

    public Monster(string name, int hp, int damage, int defense, long exp, long gold, bool isBoss = false)
    {
        Name = name; MaxHp = hp; CurrentHp = hp; Damage = damage; Defense = defense;
        RewardExp = exp; RewardGold = gold; IsBoss = isBoss;
    }

    public bool IsDead => CurrentHp <= 0;

    public int TakeDamage(int raw)
    {
        int dmg = DerivedStats.Mitigate(raw, Defense);
        CurrentHp = Math.Max(0, CurrentHp - dmg);
        return dmg;
    }
}

public sealed record Zone(
    int Id,
    string Name,
    int RequiredLevel,
    int MonsterHp,
    int MonsterDamage,
    int MonsterDefense,
    long ExpReward,
    long GoldReward,
    int KillsForBoss,
    double LootChance,
    IReadOnlyDictionary<ItemTier, int> TierWeights)
{
    public const double BossHpMult = 20, BossDmgMult = 3.0, BossRewardMult = 15;

    public Monster SpawnMonster(Random rng)
    {
        // ±10% variance keeps fights from being perfectly uniform.
        double v = 0.9 + rng.NextDouble() * 0.2;
        return new Monster($"{Name} Prowler", (int)(MonsterHp * v), MonsterDamage, MonsterDefense,
            ExpReward, GoldReward);
    }

    public Monster SpawnBoss() => new(
        $"{Name} Warden", (int)(MonsterHp * BossHpMult), (int)(MonsterDamage * BossDmgMult), MonsterDefense * 2,
        (long)(ExpReward * BossRewardMult), (long)(GoldReward * BossRewardMult), isBoss: true);
}

public static class ZoneCatalog
{
    private static readonly string[] Names =
    {
        "Ashen Outskirts", "Shattered Rift", "Gloomroot Marsh", "Sunken Bastion",
        "Ember Crown Spire", "Hollow Star Vault"
    };

    private static readonly int[] RequiredLevels = { 1, 6, 12, 19, 27, 36 };

    public static readonly IReadOnlyList<Zone> All = Build();

    public static int Count => All.Count;

    public static Zone Get(int id) => All[Math.Clamp(id, 1, All.Count) - 1];

    private static IReadOnlyList<Zone> Build()
    {
        var zones = new List<Zone>();
        for (int i = 0; i < Names.Length; i++)
        {
            // Higher zones shift loot weight toward better tiers.
            var weights = new Dictionary<ItemTier, int>
            {
                [ItemTier.Common] = Math.Max(5, 60 - i * 12),
                [ItemTier.Uncommon] = 30,
                [ItemTier.Rare] = 8 + i * 5,
                [ItemTier.Epic] = 2 + i * 3,
                [ItemTier.Legendary] = i == 0 ? 0 : i,
            };
            zones.Add(new Zone(
                Id: i + 1,
                Name: Names[i],
                RequiredLevel: RequiredLevels[i],
                MonsterHp: (int)(120 * Math.Pow(2.0, i)),
                MonsterDamage: (int)(14 * Math.Pow(1.85, i)),
                MonsterDefense: 5 + i * 15,
                ExpReward: (long)(20 * Math.Pow(2.1, i)),
                GoldReward: (long)(8 * Math.Pow(2.0, i)),
                KillsForBoss: 25,
                LootChance: 0.15,
                TierWeights: weights));
        }
        return zones;
    }
}

public static class LootTable
{
    private static readonly Dictionary<EquipSlot, string[]> BaseNames = new()
    {
        [EquipSlot.Weapon] = new[] { "Claymore", "Greataxe", "Warblade" },
        [EquipSlot.Armor] = new[] { "Cuirass", "Hauberk", "Plate" },
        [EquipSlot.Helmet] = new[] { "Helm", "Visor", "Crown" },
        [EquipSlot.Boots] = new[] { "Greaves", "Treads", "Sabatons" },
        [EquipSlot.Belt] = new[] { "Girdle", "Cinch", "Waistguard" },
        [EquipSlot.Ring] = new[] { "Band", "Signet", "Loop" },
    };

    private static readonly string[] Prefixes = { "Worn", "Tempered", "Riftforged", "Stormbound", "Sunfire" };

    public static ItemTier RollTier(Zone zone, Random rng)
    {
        int total = zone.TierWeights.Values.Sum();
        int roll = rng.Next(total);
        foreach (var (tier, w) in zone.TierWeights.OrderBy(kv => kv.Key))
        {
            if (roll < w) return tier;
            roll -= w;
        }
        return ItemTier.Common;
    }

    public static Item Generate(Zone zone, Random rng, bool fromBoss = false)
    {
        var slot = (EquipSlot)rng.Next(Enum.GetValues<EquipSlot>().Length);
        var tier = RollTier(zone, rng);
        if (fromBoss && tier < ItemTier.Rare) tier = ItemTier.Rare;

        int lvl = zone.RequiredLevel + rng.Next(0, 4);
        int budget = 4 + lvl * 2;
        var bonus = slot switch
        {
            EquipSlot.Weapon => new Attributes(budget, budget / 4, 0, 0, budget / 6),
            EquipSlot.Armor => new Attributes(budget / 4, 0, 0, budget, 0),
            EquipSlot.Helmet => new Attributes(0, 0, budget / 4, budget / 2, budget / 6),
            EquipSlot.Boots => new Attributes(0, budget / 2, 0, budget / 3, 0),
            EquipSlot.Belt => new Attributes(budget / 3, 0, 0, budget / 2, 0),
            EquipSlot.Ring => new Attributes(budget / 4, budget / 4, budget / 4, 0, budget / 2),
            _ => new Attributes()
        };
        int defense = slot is EquipSlot.Armor or EquipSlot.Helmet or EquipSlot.Boots ? lvl * 2 : 0;

        var names = BaseNames[slot];
        string name = $"{Prefixes[Math.Min((int)tier, Prefixes.Length - 1)]} {names[rng.Next(names.Length)]}";
        return new Item(name, slot, tier, lvl, bonus, defense);
    }
}
