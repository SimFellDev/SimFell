namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 164: "Your attacks and abilities have +20% additional critical strike chance when striking targets below 30%
/// health." Effect 2204 (CritChance +0.2).
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
