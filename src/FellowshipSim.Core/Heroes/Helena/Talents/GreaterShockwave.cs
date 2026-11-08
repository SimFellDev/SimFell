using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 643: "Your Shockwave grants an additional 10% Toughness and deals up to 30% increased damage based on how much
/// Toughness you have." Effect 1272 talent IncreasedDamage DamageMultiplier 1.3; game data
/// Warmaster.MeleeAoeSlow.Talent.IncreasedToughnessAndDamage.AdditionalToughnessBasedOnMax 0.1. The bonus is taken to grow
/// in step with the share of the maximum Toughness, read at the cast.
/// </summary>
public partial class GreaterShockwave : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena || hero.GetAbility<Shockwave>() is not { } shockwave) return;
        shockwave.BonusToughnessShare += IncreasedToughnessAndDamageAdditionalToughnessBasedOnMax;
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is Shockwave) cast.DamageMultiplier *= 1.0 + IncreasedToughnessAndDamageDamageMultiplierDamageMultiplierAtFullToughness * helena.ToughnessShare;
        };
    }
}
