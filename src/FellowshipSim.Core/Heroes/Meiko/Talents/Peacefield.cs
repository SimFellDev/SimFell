namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 174: "Your Serenity applies Peacefield on you, healing you for 80% of the initial heal's value over 9 seconds."
/// It changes no damage Meiko deals.
/// </summary>
public class Peacefield : Talent
{
    public const int TalentId = 174;
    public const string TalentName = "Peacefield";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
