using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 98: "Your Spirited Vortex deals 20% more damage. Each time you perform a finisher ability while Spirited Vortex is
/// active it grants you +20% dodge chance, stacking up to 3 times. All stacks of the bonus dodge are consumed upon
/// successfully dodging an attack" (Meiko.DurationalAoeDamageBuff.Talent.IncreasedDamage.DamageMultiplier 1.2 on ability
/// 1120 and effect 1569). The dodge is effect 2454 (maxStacks 3, DodgeChance +0.2). Enemy attacks are not simulated, so
/// the stacks stay.
/// </summary>
public class HarshWinds : Talent
{
    public const int TalentId = 98;
    public const string TalentName = "Harsh Winds";
    public const double VortexDamage = 1.2;
    public const int BuffEffectId = 2454;
    public const int MaxStacks = 3;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, maxStacks: MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Meiko meiko || hero.GetAbility<SpiritedVortex>() is not { } vortex) return;
        vortex.AddDamageMultiplier(VortexDamage);
        meiko.Finished += (ctx, _, _) =>
        {
            if (hero.Auras.HasAura(SpiritedVortex.Buff, ctx.CurrentTime)) hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
