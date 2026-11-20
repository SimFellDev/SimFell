namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 208: "The cooldown of your Shields Up and Shield Throw abilities are reduced relative to the amount of damage you
/// mitigate with Toughness."
/// </summary>
public partial class ShieldMastery : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
