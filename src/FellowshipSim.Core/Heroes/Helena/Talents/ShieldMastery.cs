namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 208: "The cooldown of your Shields Up and Shield Throw abilities are reduced relative to the amount of damage you
/// mitigate with Toughness." The engine has no damage taken, so nothing is mitigated and no cooldown changes.
/// </summary>
public class ShieldMastery : Talent
{
    public const int TalentId = 208;
    public const string TalentName = "Shield Mastery";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
