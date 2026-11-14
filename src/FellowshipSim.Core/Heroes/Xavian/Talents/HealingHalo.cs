namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 574: "Your Shining Halo heals one ally standing in its radius for 30% of the total damage it dealt each tick."
/// </summary>
public partial class HealingHalo : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
