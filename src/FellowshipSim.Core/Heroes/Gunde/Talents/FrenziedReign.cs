using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 750: "Your Reign in Blood now causes the direct damage of your abilities that apply Rend to transfer an
/// additional damage to Rend." Combat logs (n7zxK8harZT2wjDB fight 9 @ 16.402): the Rend share of Heart Splitter in
/// Reign in Blood with Serrated Edge and Superior Serration is the sum of the shares of Heart Splitter, Reign in Blood,
/// Frenzied Reign, Serrated Edge and Superior Serration.
/// </summary>
public partial class FrenziedReign : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var reign = hero.GetAbility<ReignInBlood>();
        if (reign != null) reign.ShareBonus += AddedBonusAddedTransfer;
    }
}
