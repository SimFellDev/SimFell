using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 772 / effect 3211: "Each Rend that is consumed by your Slaughter grants you Spirit for a time, up
/// to a maximum." Blueprint GA_Gunde_Passive_Talent_InstantAoeDot_StacksToHaste (build 26320967): a Slaughter grants a
/// stack per Rend stack it consumed, summed over its enemies. The buff stacks by source with the engine's defaults: a
/// second Slaughter adds its stacks up to <see cref="MaxStacks"/> and refreshes the duration, and all stacks expire
/// together. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class Massacre : Talent
{
    public const int EffectId = 3211;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, Constants.StacksToHasteDuration, Constants.StacksToHasteMaxStacks,
        StatModifier.Add(StatType.Spirit, Constants.StacksToHasteAdditionalHastePerStack));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.GetAbility<Slaughter>()!.Consumed += (ctx, stacks) =>
            hero.Auras.Apply(Buff, ctx.CurrentTime, stacks: Math.Min(stacks, Constants.StacksToHasteMaxStacks));
    }
}
