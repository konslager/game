namespace IdleMMO.Core;

public enum SkillKind { Damage, Heal, Shield }

/// <summary>Static skill definition. Cooldowns are in combat ticks.</summary>
public sealed record SkillDef(
    string Id,
    string Name,
    SkillKind Kind,
    int CooldownTicks,
    int ManaCost,
    double Power,           // Damage: weapon-damage multiplier. Heal/Shield: fraction of MaxHp.
    int UnlockLevel);

public sealed record ClassDef(string Id, string Name, Attributes BaseAttributes, Attributes GrowthPerLevel, IReadOnlyList<SkillDef> Skills);

public static class ClassCatalog
{
    public static readonly ClassDef Gladiator = new(
        "gladiator", "Gladiator",
        BaseAttributes: new Attributes(str: 50, agi: 20, intel: 5, vit: 30, luck: 10),
        GrowthPerLevel: new Attributes(str: 2, agi: 1, intel: 0, vit: 2, luck: 0),
        Skills: new[]
        {
            new SkillDef("cleave",       "Cleave",           SkillKind.Damage, 3,  5,  1.6, 1),
            new SkillDef("sunder",       "Sundering Strike", SkillKind.Damage, 5,  10, 2.2, 3),
            new SkillDef("ruin",         "Ruin Strike",      SkillKind.Damage, 8,  20, 3.0, 5),
            new SkillDef("shockwave",    "Shockwave",        SkillKind.Damage, 12, 30, 4.0, 10),
            new SkillDef("second_wind",  "Second Wind",      SkillKind.Heal,   15, 15, 0.35, 1),
            new SkillDef("iron_guard",   "Iron Guard",       SkillKind.Shield, 20, 15, 0.25, 7),
        });

    public static readonly IReadOnlyDictionary<string, ClassDef> All =
        new Dictionary<string, ClassDef> { [Gladiator.Id] = Gladiator };

    public static ClassDef Get(string id) =>
        All.TryGetValue(id, out var c) ? c : throw new ArgumentException($"Unknown class '{id}'", nameof(id));
}
