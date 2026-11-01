namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 645: "Your Block Chance is increased by 5%. You gain another 5% Block Chance while Shields Up is active." The engine
/// has no attacks on Helena, so Block Chance changes nothing.
/// </summary>
public class GuardedVeteran : Talent
{
    public const int TalentId = 645;
    public const string TalentName = "Guarded Veteran";
    public const double BlockChance = 0.05;
    public const double ShieldsUpBlockChance = 0.05;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
