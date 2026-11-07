namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 164: attacks and abilities have additional critical strike chance when striking targets below a health
/// threshold. Effect 2204 (CritChance).
/// </summary>
public class LastLights : Talent
{
    public const int TalentId = 164;
    public const string TalentName = "Last Lights";
    public const double CritChance = 0.2;
    public const double HealthThreshold = 0.3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        hero.TargetCritChanceBonuses.Add((target, _) => target.HealthFraction < HealthThreshold ? CritChance : 0.0);
}
