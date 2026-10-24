namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>Talent 749: "Your Blood Arc's Serrated Edge grants an additional +10% damage applied to Rend."</summary>
public class SuperiorSerration : Talent
{
    public const int TalentId = 749;
    public const string TalentName = "Superior Serration";
    public const double RendShareBonus = 0.1;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Gunde)hero).SerratedEdgeShare += RendShareBonus;
}
