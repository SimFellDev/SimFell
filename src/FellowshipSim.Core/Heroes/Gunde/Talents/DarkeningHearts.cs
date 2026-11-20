using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 757: "Your Heart Splitter deals more Direct Damage. Your Heart Splitter has increased Critical Strike chance
/// against targets with low health." Crit chance beyond a guaranteed crit is a grievous crit.
/// </summary>
public partial class DarkeningHearts : Talent
{
    public const double LowHealthThreshold = 0.3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var heartSplitter = hero.GetAbility<HeartSplitter>();
        if (heartSplitter == null) return;
        heartSplitter.AddDamageMultiplier(Constants.IncreasedDirectDamageDamageScale);
        heartSplitter.TargetCritChanceBonuses.Add(target => target.HealthFraction <= LowHealthThreshold ? Constants.LowHealthTargetAddedCriticalStrikeChance : 0.0);
    }
}
