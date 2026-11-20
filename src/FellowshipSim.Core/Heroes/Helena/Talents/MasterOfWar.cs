namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 647: "Each time you Block an attack, the cooldown of Grand Melee is reduced by 0.2 seconds."
/// </summary>
public partial class MasterOfWar : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
