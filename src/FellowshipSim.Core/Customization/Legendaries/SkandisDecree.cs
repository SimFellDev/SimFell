using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 396: "Your Bursting Ice lasts longer". Combat logs: the debuff (effect 1395) is applied with a longer
/// duration with it than without it (AY1m6jPKTZyx4vz2 fight 9, NhBnFXmy3bJg9Cav fights 32 and 33).
/// </summary>
public partial class SkandisDecree : Legendary
{
    public override void Bind(Hero hero)
    {
        if (hero is Rime rime && rime.GetAbility<BurstingIce>() is { } burstingIce) burstingIce.BonusDurationSeconds += IncreasedDurationDurationInSeconds;
    }
}
