using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 616: "Your Engulfing Flames also causes the afflicted target to take more damage from you for each
/// Engulfing Flames that is active on them." Combat logs (Fc8v6yWxD14GQMnV fight 19, YC861NTRgLbmw4Qa fight 31) show its
/// wearer dealing more damage to enemies with Engulfing Flames on them. Each Engulfing Flames multiplies the bonus (per
/// the user): <see cref="Constants.BoostsIncomingDamageDamageMultiplier"/>^K with K of them.
/// </summary>
public partial class DevouringFlame : Legendary
{
    public override void Bind(Hero hero)
    {
        if (hero is not Ardeos ardeos || ardeos.GetAbility<EngulfingFlames>()?.Dot is not { } engulfing) return;
        hero.TargetDamageMultipliers.Add((target, _) => Math.Pow(Constants.BoostsIncomingDamageDamageMultiplier, ardeos.Dots.Count(target, engulfing)));
    }
}
