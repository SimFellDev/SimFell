using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 442: "When you deal damage to enemies with low health, the damage bonus of Maiden of Death is increased"
/// (effect 2194's third DamageScale modifier). UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class MaidensDoom : Talent
{
    public const double HealthThreshold = 0.30;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var maiden = hero.GetAbility<MaidenOfDeath>();
        if (maiden != null) maiden.DamageAgainst = enemy => enemy.HealthFraction <= HealthThreshold ? Constants.ChargedResourceRefillSelfBuffTalentIncreasedDamageDamageMultiplierAtLowHealth : MaidenOfDeath.BuffDamage;
    }
}
