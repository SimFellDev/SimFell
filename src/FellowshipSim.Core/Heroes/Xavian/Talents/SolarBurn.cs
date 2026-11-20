using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 570: "The main target of Solar Blades is afflicted by Sunburn, dealing 100% of Solar Blades' damage over 9
/// seconds. Sunburn causes the enemy to deal 10% reduced damage to you for the duration." Effect 2660 (Sunburn, 9s, period
/// 1.5, a damage over time). Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 11.122): a tick deals a sixth of the main target's
/// Solar Blades hit, crit included (2302 -> 384).
/// </summary>
public partial class SolarBurn : Talent
{
    public const int DotEffectId = 2660;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        var dot = new XavianDot(xavian, DotEffectId, "Sunburn", Constants.AtTargetInstantAoeTalentAddsDotDotPeriod, Constants.AtTargetInstantAoeTalentAddsDotDotDuration);
        hero.Register(dot.Debuff);
        xavian.SolarBlades.MainTargetHit += (ctx, enemy, dealt) => dot.Apply(ctx, enemy, dealt * Constants.AtTargetInstantAoeTalentAddsDotDotDamageScaler);
    }
}
