namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 647: "Each time you Block an attack, the cooldown of Grand Melee is reduced by 0.2 seconds." The engine has no
/// attacks on Helena, so nothing blocks.
/// </summary>
public class MasterOfWar : Talent
{
    public const int TalentId = 647;
    public const string TalentName = "Master of War";
    public const double GrandMeleeReductionSeconds = 0.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
