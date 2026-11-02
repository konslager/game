namespace IdleMMO.Core;

/// <summary>Primary hero attributes (also used as item bonuses).</summary>
public sealed class Attributes
{
    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Intelligence { get; set; }
    public int Vitality { get; set; }
    public int Luck { get; set; }

    public Attributes() { }

    public Attributes(int str, int agi, int intel, int vit, int luck)
    {
        Strength = str; Agility = agi; Intelligence = intel; Vitality = vit; Luck = luck;
    }

    public Attributes Clone() => new(Strength, Agility, Intelligence, Vitality, Luck);

    public void Add(Attributes o)
    {
        Strength += o.Strength; Agility += o.Agility; Intelligence += o.Intelligence;
        Vitality += o.Vitality; Luck += o.Luck;
    }

    public Attributes Scale(double f) => new(
        (int)Math.Round(Strength * f), (int)Math.Round(Agility * f), (int)Math.Round(Intelligence * f),
        (int)Math.Round(Vitality * f), (int)Math.Round(Luck * f));

    public int Total => Strength + Agility + Intelligence + Vitality + Luck;
}

/// <summary>Combat values derived from attributes + gear. Pure function of inputs.</summary>
public readonly record struct DerivedStats(
    int PhysicalDamage,
    int MagicDamage,
    int Defense,
    double CritChance,
    double CritDamage,
    int MaxHp,
    int MaxMana,
    double HpRegen,
    double ManaRegen,
    double AttackSpeed)
{
    public const double CritChanceCap = 0.60;

    public static DerivedStats From(Attributes a, int gearDefense) => new(
        PhysicalDamage: 10 + a.Strength * 2,
        MagicDamage: 5 + a.Intelligence * 2,
        Defense: a.Vitality / 5 + gearDefense,
        CritChance: Math.Min(CritChanceCap, 0.05 + a.Luck * 0.002),
        CritDamage: 1.5 + a.Agility * 0.002,
        MaxHp: 200 + a.Vitality * 10,
        MaxMana: 50 + a.Intelligence * 5,
        HpRegen: a.Vitality * 0.1,
        ManaRegen: 1 + a.Intelligence * 0.05,
        AttackSpeed: 1.0 + a.Agility * 0.005);

    /// <summary>Damage after defense mitigation: def/(def+100) reduction, min 1.</summary>
    public static int Mitigate(int raw, int defense) =>
        Math.Max(1, (int)Math.Round(raw * (100.0 / (100.0 + Math.Max(0, defense)))));
}
