using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 78: "Your Skull Crusher direct damage deals a share of the damage dealt to enemies near your target. (Damage
/// reduced beyond a number of targets)." Logged as ability 2061 (whose own text states another share). Combat logs
/// (fXyzwGcQkTrLD34R fight 4, 1p7AXmHfwV9CYMxR fight 30; 587 hits): each nearby enemy takes <see cref="DamageScaler"/> of the
/// physical hit dealt, crit included; it never crits itself.
/// </summary>
public partial class Sledgehammer : Talent
{
    public const int DamageId = 2061;

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
            double falloff = nearby.Count > TargetThresholdForDamageScale ? Math.Sqrt((double)TargetThresholdForDamageScale / nearby.Count) : 1.0;
            for (int i = 0; i < nearby.Count; i++)
            {
                ctx.ApplyDamage(hero, nearby[i], dealt * DamageScaler * falloff, DamageId, TalentName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
