using FellowshipSim.Core.Heroes.Helena;
using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 625 (Helena's Warmaster Helm, item 5899): "Your Hold The Line ability now has 2 charges and gains Cooldown
/// Acceleration equal to your Haste" (legendary-power 625; ability 975 cooldownHasteGrantedBy Iron Sentinel).
/// </summary>
public class IronSentinel : Legendary
{
    public const int PowerId = 625;
    /// <summary>Warmaster Helm.</summary>
    public const int HelmItemId = 5899;
    public const int HoldTheLineCharges = 2;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Iron Sentinel";
    public override string HeroName => "Helena";
    public override IReadOnlyList<int> ItemIds => [HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is Helena && hero.GetAbility<HoldTheLine>() is { } holdTheLine) holdTheLine.UseChargesWithHaste(HoldTheLineCharges);
    }
}
