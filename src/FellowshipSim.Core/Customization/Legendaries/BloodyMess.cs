using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 697: Grim Carve spins additional times. Each extra spin comes as long after the one before as the
/// others. UNVERIFIED: the timing of the extra spins.
/// </summary>
public partial class BloodyMess : Legendary
{
    public override void Bind(Hero hero)
    {
        if (hero is Gunde gunde && gunde.GetAbility<GrimCarve>() is { } grimCarve) grimCarve.SpinCount += Constants.AdditionalTicksAmount;
    }
}
