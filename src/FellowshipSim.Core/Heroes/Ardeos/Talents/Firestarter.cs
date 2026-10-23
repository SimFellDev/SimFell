using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 149: "All damage over time effects applied to enemies by your abilities gain +20% critical strike chance. Fire
/// Frogs and Fire Ball also gain a 20% chance to critically strike with their damage over time effects." Fire Ball and
/// Fire Frogs effects then crit as the others do. UNVERIFIED: no combat log has this talent.
/// </summary>
public class Firestarter : Talent
{
    public const int TalentId = 149;
    public const string TalentName = "Firestarter";
    public const double CritChance = 0.20;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        foreach (var dot in ((Ardeos)hero).AllDots)
        {
            dot.CritChanceBonus += CritChance;
            dot.TicksCanCrit = true;
        }
    }
}
