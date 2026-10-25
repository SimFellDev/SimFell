using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 396 (Rime's Harbinger of Winter Leggings, item 5900): "Your Bursting Ice lasts 2 seconds longer"
/// (legendary-power 396, datamine export 26136736). Combat logs: the debuff (effect 1395) is applied with a duration of
/// 5000ms against 3000ms without it (AY1m6jPKTZyx4vz2 fight 9, NhBnFXmy3bJg9Cav fights 32 and 33).
/// </summary>
public class SkandisDecree : Legendary
{
    public const int PowerId = 396;
    public const int LegsItemId = 5900;
    public const double DurationBonusSeconds = 2.0;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Skandi's Decree";
    public override string HeroName => "Rime";
    public override IReadOnlyList<int> ItemIds => [LegsItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Rime rime || rime.GetAbility<BurstingIce>() is not { } burstingIce) return;
        burstingIce.DurationBonusSeconds += DurationBonusSeconds;
    }
}
