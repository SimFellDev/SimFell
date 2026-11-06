using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 757: "Your Heart Splitter deals more Direct Damage. Your Heart Splitter has increased Critical Strike chance
/// against targets with low health." Crit chance beyond a guaranteed crit is a grievous crit.
/// </summary>
public class DarkeningHearts : Talent
{
    public const int TalentId = 757;
    public const string TalentName = "Darkening Hearts";
    public const double DamageMultiplier = 1.1;
    public const double LowHealthThreshold = 0.3;
    public const double LowHealthCritChance = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var heartSplitter = hero.GetAbility<HeartSplitter>();
        if (heartSplitter == null) return;
        heartSplitter.AddDamageMultiplier(DamageMultiplier);
        heartSplitter.TargetCritChanceBonuses.Add(target => target.HealthFraction <= LowHealthThreshold ? LowHealthCritChance : 0.0);
    }
}
