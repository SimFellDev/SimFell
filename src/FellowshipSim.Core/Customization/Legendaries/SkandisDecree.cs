using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 396 (Rime's Harbinger of Winter Leggings, item 5900): "Your Bursting Ice lasts 2 seconds longer"
/// (datamine export 26136736). UNVERIFIED: no replay has checked the longer debuff's pulses yet (worn in
/// AY1m6jPKTZyx4vz2 fights 8 and 9, 4Ln6PQZCRzc7Wpta fight 72).
/// </summary>
public class SkandisDecree : Legendary
{
    public const int PowerId = 396;
    /// <summary>Harbinger of Winter Leggings.</summary>
    public const int LegsItemId = 5900;
    public const double BurstingIceBonusSeconds = 2.0;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Skandi's Decree";
    public override string HeroName => "Rime";
    public override IReadOnlyList<int> ItemIds => [LegsItemId];

    public override void Bind(Hero hero)
    {
        if (hero is Rime rime && rime.GetAbility<BurstingIce>() is { } burstingIce) burstingIce.BonusDurationSeconds += BurstingIceBonusSeconds;
    }
}
