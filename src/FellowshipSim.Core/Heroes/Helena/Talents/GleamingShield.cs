using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 641: "Each time you use Shield Throw, Shield Slam, Shockwave, and Shields Up, you gain 3.0% increased Armor for 3
/// seconds, stacking up to 10 times. Upon expiration, 1 stack is lost. Your Shield Slam deals additional damage to all
/// targets hit equal to 10% of your total Armor value." The buff is effect 3096; game data
/// Helena.Talent.IncreasedArmorFromAbilities.MeleeCleave.AdditionalDamageBasedOnArmor 0.1. Shield Slam's add is 10% of the
/// build's Armor with 3% more per stack.
/// </summary>
public partial class GleamingShield : Talent
{
    public const int BuffEffectId = 3096;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, TalentDuration, Constants.MaxStacks) with { ExpiryRemovesOneStack = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena) return;
        helena.ArmorBonuses.Add(now => Constants.AdditionalArmorPerStack * hero.Auras.GetStacks(Buff, now));
        if (hero.GetAbility<ShieldSlam>() is { } shieldSlam) shieldSlam.ArmorDamageShare += Constants.IncreasedArmorFromAbilitiesMeleeCleaveAdditionalDamageBasedOnArmor;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is ShieldThrow or ShieldSlam or Shockwave or ShieldsUp) hero.Auras.Apply(Buff, now);
        };
    }
}
