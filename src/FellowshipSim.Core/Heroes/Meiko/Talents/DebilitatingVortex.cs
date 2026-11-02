namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 97: "Enemies hit by Spirited Vortex have 20% reduced attack speed for 5 seconds."
/// </summary>
public class DebilitatingVortex : Talent
{
    public const int TalentId = 97;
    public const string TalentName = "Debilitating Vortex";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
