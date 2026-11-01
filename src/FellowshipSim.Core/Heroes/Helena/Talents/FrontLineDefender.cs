namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 646: "Your Toughness reduces damage taken by an additional 5% on each Toughness Tier." The engine has no damage
/// taken, so nothing changes.
/// </summary>
public class FrontLineDefender : Talent
{
    public const int TalentId = 646;
    public const string TalentName = "Front Line Defender";
    public const double DamageReductionPerTier = 0.05;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
