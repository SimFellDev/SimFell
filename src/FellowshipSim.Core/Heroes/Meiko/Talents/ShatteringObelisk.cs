using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 415: "The Sibling of Earth created by your Twin Souls: Bulwark ability has 50% increased health and duration. The
/// Sibling of Earth accumulates 15% of all damage it takes and pulses it out to nearby enemies up to 3 times, when reaching
/// 65% Health, 30% Health, and finally upon being destroyed. If Sibling of Earth expires due to its duration, it deals 15% of
/// any remaining health it has." Effect 1491 lasts 30 seconds with it
/// (Meiko.TauntTotem.Talent.IncreasedHealthAndDuration.DurationMultiplier 1.5). The pulses come from damage the sibling
/// takes, and the sibling's health from Meiko's maximum health: neither is simulated, so the engine deals no pulse.
/// </summary>
public class ShatteringObelisk : Talent
{
    public const int TalentId = 415;
    public const string TalentName = "Shattering Obelisk";
    public const double DurationMultiplier = 1.5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<TwinSoulsBulwark>() is { } bulwark) bulwark.SiblingSeconds = TwinSoulsBulwark.BuffSeconds * DurationMultiplier;
    }
}
