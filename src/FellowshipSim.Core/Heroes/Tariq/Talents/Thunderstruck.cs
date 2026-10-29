using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>Talent 89: "Your Chain Lightning bounces 3 additional times." Combat logs: 9-hit chains (q3y7pWgXrjN2dftw fight 12).</summary>
public class Thunderstruck : Talent
{
    public const int TalentId = 89;
    public const string TalentName = "Thunderstruck";
    public const int ExtraBounces = 3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is { } chainLightning) chainLightning.ExtraHits += ExtraBounces;
    }
}
