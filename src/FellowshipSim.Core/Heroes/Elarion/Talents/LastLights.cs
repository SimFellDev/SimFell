namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 164: attacks and abilities have additional critical strike chance when striking targets below a health
/// threshold. Effect 2204 (CritChance).
/// </summary>
public partial class LastLights : Talent
{
    public const double HealthThreshold = 0.3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        hero.TargetCritChanceBonuses.Add((target, _) => target.HealthFraction < HealthThreshold ? AdditionalCritChance : 0.0);
}
