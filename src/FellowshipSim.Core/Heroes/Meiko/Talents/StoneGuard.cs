namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 446: "Any time you gain health from your self healing abilities, 10% of the healing amount also replenishes your
/// Stone Shield's health. When you have less than 3 stones active, fully replenishing a Stone will add a new one to your
/// active Stone Shield."
/// </summary>
public partial class StoneGuard : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
