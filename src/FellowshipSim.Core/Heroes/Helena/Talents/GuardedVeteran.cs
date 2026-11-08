namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 645: "Your Block Chance is increased by 5%. You gain another 5% Block Chance while Shields Up is active."
/// </summary>
public partial class GuardedVeteran : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
