namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 103: "Each cast of Shatter Earth reduces the cooldown of Twin Souls: Bulwark by 20%." Twin Souls: Bulwark deals
/// no damage, so the talent changes none.
/// </summary>
public class Earthbourne : Talent
{
    public const int TalentId = 103;
    public const string TalentName = "Earthbourne";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
