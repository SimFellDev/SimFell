namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 446: "Any time you gain health from your self healing abilities, 10% of the healing amount also replenishes your
/// Stone Shield's health. When you have less than 3 stones active, fully replenishing a Stone will add a new one to your
/// active Stone Shield" (Meiko.Talent.SelfHealToStoneHealth.HealMultiplier 0.1). Self-healing and the stones' health are not
/// simulated, so the talent changes nothing in a sim.
/// </summary>
public class StoneGuard : Talent
{
    public const int TalentId = 446;
    public const string TalentName = "Stone Guard";
    public const double HealToStoneHealth = 0.1;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
