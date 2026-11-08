using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 772 / effect 3211: "Each Rend that is consumed by your Slaughter grants you Spirit for a time, up
/// to a maximum." A stack per enemy whose Rend Slaughter consumes. UNVERIFIED: no combat log has this
/// talent.
/// </summary>
public partial class Massacre : Talent
{
    public const int EffectId = 3211;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, Duration, MaxStacks,
        StatModifier.Add(StatType.Spirit, AdditionalHastePerStack));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.GetAbility<Slaughter>()!.Consumed += (ctx, _, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
