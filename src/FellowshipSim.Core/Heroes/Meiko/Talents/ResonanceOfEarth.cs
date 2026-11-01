namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 90: "When you activate Stone Shield you take 20% reduced damage for 8 seconds." It changes no damage Meiko deals.
/// </summary>
public class ResonanceOfEarth : Talent
{
    public const int TalentId = 90;
    public const string TalentName = "Resonance of Earth";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
