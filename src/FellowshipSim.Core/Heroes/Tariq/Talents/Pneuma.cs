using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 81: "Your Culling Strike generates a Spirit Point." (Culling Strike generates SpiritPoints.)
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public class Pneuma : Talent
{
    public const int TalentId = 81;
    public const string TalentName = "Pneuma";
    public const double SpiritPoints = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<CullingStrike>() is { } cullingStrike) cullingStrike.Struck += ctx => hero.GainResource(Hero.SpiritPoints, SpiritPoints, ctx);
    }
}
