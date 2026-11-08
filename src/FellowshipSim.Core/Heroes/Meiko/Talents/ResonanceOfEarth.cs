namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 90: "When you activate Stone Shield you take 20% reduced damage for 8 seconds."
/// </summary>
public partial class ResonanceOfEarth : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
