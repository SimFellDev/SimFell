using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 149: all damage over time effects the hero's abilities apply to enemies gain critical strike chance. Fire Frogs
/// and Fire Ball also gain a chance to critically strike with their damage over time effects. Fire Ball and
/// Fire Frogs effects then crit as the others do. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class Firestarter : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        ((Ardeos)hero).ForEveryDot(dot =>
        {
            dot.CritChanceBonus += IncreasedDotCritChanceCritChanceIncrease;
            dot.TicksCanCrit = true;
        });
    }
}
