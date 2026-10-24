using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 78: "Your Skull Crusher direct damage deals 10% of the damage dealt to enemies near your target. (Damage
/// reduced beyond 8 targets)." Logged as ability 2061 (whose own text says 30%). Combat logs (fXyzwGcQkTrLD34R fight 4,
/// 1p7AXmHfwV9CYMxR fight 30; 587 hits): each nearby enemy takes exactly 10% of the physical hit dealt, crit included;
/// it never crits itself.
/// </summary>
public class Sledgehammer : Talent
{
    public const int TalentId = 78;
    public const string TalentName = "Sledgehammer";
    public const int DamageId = 2061;
    public const double Fraction = 0.10;
    public const double TargetThreshold = 8;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<SkullCrusher>() is not { } skullCrusher) return;
        skullCrusher.Landed += (ctx, target, dealt) =>
        {
            if (dealt <= 0.0) return;
            var nearby = ctx.Outcomes.SelectTargets(DamageId, ctx.ActiveEnemiesExcept(target), ctx);
            if (nearby.Count == 0) return;
            double falloff = nearby.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / nearby.Count) : 1.0;
            for (int i = 0; i < nearby.Count; i++)
            {
                ctx.ApplyDamage(hero, nearby[i], dealt * Fraction * falloff, DamageId, TalentName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
