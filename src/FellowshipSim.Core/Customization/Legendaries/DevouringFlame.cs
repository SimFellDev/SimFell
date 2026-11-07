using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 616 (Ardeos's Draconic Bracers of the Devouring Flame, item 5225; item 5900): "Your Engulfing Flames
/// also causes the afflicted target to take more damage from you for each Engulfing Flames that is active on them."
/// Item 5900 grants the same power: combat logs (Fc8v6yWxD14GQMnV fight 19, YC861NTRgLbmw4Qa fight 31) show its wearer
/// dealing more damage to enemies with Engulfing Flames on them.
/// </summary>
public class DevouringFlame : Legendary
{
    public const int PowerId = 616;
    public const int BracersItemId = 5225;
    public const int HelmItemId = 5900;
    public const double DamagePerInstance = 0.07;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Devouring Flame";
    public override string HeroName => "Ardeos";
    public override IReadOnlyList<int> ItemIds => [BracersItemId, HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Ardeos ardeos || ardeos.GetAbility<EngulfingFlames>()?.Dot is not { } engulfing) return;
        hero.TargetDamageMultipliers.Add((target, _) => 1.0 + DamagePerInstance * ardeos.Dots.Count(target, engulfing));
    }
}
