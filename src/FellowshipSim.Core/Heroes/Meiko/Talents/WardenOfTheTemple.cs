using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 102: "Each time you consume Earthfall or Stormfall, you gain 1 stack of Warden of the Temple. At 3 stacks, your
/// next Palm Finisher is enhanced. Double Palm Strike: Deals 300% increased damage and grants 25 Serenity charges.
/// Spirited Vortex: Deals 50% increased damage and its Dodge Bonus is increased to +30%." Effect 2447 (maxStacks 3);
/// Meiko.Talent.ConsumedBuffsToDefensiveBuff.MeleeDamageImmediateSelfBuff.DamageMultiplier 4 and
/// .InstantSelfDefensive.NumOfStacks 25. Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 38.902): the Palm finisher removes the
/// buff.
/// </summary>
public partial class WardenOfTheTemple : Talent
{
    public const int BuffEffectId = 2447;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, maxStacks: Constants.ConsumedBuffsToDefensiveBuffMaxStacksForActivation);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Meiko meiko) return;
        meiko.ChargeConsumed += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not (DoublePalmStrike or SpiritedVortex) || hero.Auras.GetStacks(Buff, cast.Time) < Constants.ConsumedBuffsToDefensiveBuffMaxStacksForActivation) return;
            hero.Auras.Remove(Buff, cast.Time);
            if (cast.Ability is DoublePalmStrike)
            {
                cast.DamageMultiplier *= Constants.TalentConsumedBuffsToDefensiveBuffMeleeDamageImmediateSelfBuffDamageMultiplier;
                hero.Auras.Apply(Serenity.Stacks, cast.Time, Constants.NumOfStacks);
            }
            else
            {
                cast.DamageMultiplier *= Constants.TalentConsumedBuffsToDefensiveBuffDurationalAoeDamageBuffDamageMultiplier;
            }
        };
    }
}
