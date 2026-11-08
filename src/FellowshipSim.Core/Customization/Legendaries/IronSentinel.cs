using FellowshipSim.Core.Heroes.Helena;
using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 625: "Your Hold The Line ability now has 2 charges and gains Cooldown Acceleration equal to your Haste"
/// (ability 975 cooldownHasteGrantedBy Iron Sentinel).
/// </summary>
public partial class IronSentinel : Legendary
{
    public override void Bind(Hero hero)
    {
        if (hero is Helena && hero.GetAbility<HoldTheLine>() is { } holdTheLine) holdTheLine.UseChargesWithHaste(ChargesNumCharges);
    }
}
