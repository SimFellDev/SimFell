namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>Talent 749: "Your Blood Arc's Serrated Edge grants additional damage applied to Rend."</summary>
public partial class SuperiorSerration : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Gunde)hero).SerratedEdgeShare += AddedTransferDotTransferBonus;
}
