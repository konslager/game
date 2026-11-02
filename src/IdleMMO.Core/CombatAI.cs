namespace IdleMMO.Core;

/// <summary>
/// Priority tree for auto-combat:
///   1. HP &lt; 30%  → heal skill
///   2. HP &lt; 50% and no shield → shield skill
///   3. Strongest ready damage skill
///   4. Auto-attack (null)
/// </summary>
public static class CombatAI
{
    public const double HealThreshold = 0.30;
    public const double ShieldThreshold = 0.50;

    public static SkillDef? Decide(Player p)
    {
        double hpPct = (double)p.CurrentHp / p.Stats.MaxHp;
        var ready = p.UnlockedSkills.Where(p.IsReady).ToList();

        if (hpPct < HealThreshold)
        {
            var heal = ready.Where(s => s.Kind == SkillKind.Heal).MaxBy(s => s.Power);
            if (heal != null) return heal;
        }

        if (hpPct < ShieldThreshold && p.Shield == 0)
        {
            var shield = ready.Where(s => s.Kind == SkillKind.Shield).MaxBy(s => s.Power);
            if (shield != null) return shield;
        }

        return ready.Where(s => s.Kind == SkillKind.Damage).MaxBy(s => s.Power);
    }
}
