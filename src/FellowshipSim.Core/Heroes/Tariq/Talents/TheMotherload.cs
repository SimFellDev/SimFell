using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 672: "Your Chain Lightning deals +65% increased damage for each unique enemy other than your target that it
/// hits." UNVERIFIED: no combat log has this talent; each hit counts the other enemies hit so far, itself included.
/// </summary>
public class TheMotherload : Talent
{
    public const int TalentId = 672;
    public const string TalentName = "The Motherload";
    public const double DamagePerEnemy = 0.65;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is { } chainLightning) chainLightning.DamagePerOtherEnemy += DamagePerEnemy;
    }
}
