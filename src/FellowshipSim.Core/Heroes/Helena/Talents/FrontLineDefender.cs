namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 646: "Your Toughness reduces damage taken by an additional 5% on each Toughness Tier."
/// </summary>
public partial class FrontLineDefender : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
