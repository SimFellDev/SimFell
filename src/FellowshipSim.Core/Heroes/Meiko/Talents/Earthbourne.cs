namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 103: "Each cast of Shatter Earth reduces the cooldown of Twin Souls: Bulwark by 20%."
/// </summary>
public partial class Earthbourne : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
