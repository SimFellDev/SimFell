using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 44 / effect 2308: Cold Snap grants a stack; the next Glacial Blast is cast with added Haste, the
/// next Ice Comet lands sooner, and the one consuming it gets added crit chance (talent text and game data; the buff's own text gives a different crit bonus). Talon Strike and
/// Rising Talons consume it too (Icy Talons). Combat logs (GWChJ8P7tn6fHgV3 fights 20 and 22) have the haste. UNVERIFIED: the
/// crit bonus; Ice Comet's impact delay is not modeled.
/// </summary>
public partial class IcyFlow : Talent
{
    public const int EffectId = 2308;
    public const int MaxStacks = 2;

    public static readonly AuraDef Buff = AuraDef.Effect("Icy Flow", EffectId, Constants.InstantSingleDamageTalentReducesCastTimeOnOrbSpenderBuffDuration, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    /// <summary>
    /// Cold Snap grants a stack; the next spender uses it up for crit, a Glacial Blast cast with it is faster and an Ice
    /// Comet cast with it falls faster.
    /// </summary>
    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is ColdSnap) hero.Auras.Apply(Buff, now);
        };
        // The next Glacial Blast or Ice Comet (Talon Strike and Rising Talons count; Cold Shower's comet too, but not
        // Avalanche's extra comets).
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not (GlacialBlast or IceComet) || cast.Source is Avalanche || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.CritChance += Constants.InstantSingleDamageTalentReducesCastTimeOnOrbSpenderAddedCritChance;
            if (cast.Ability is IceComet) cast.DelayReductionSeconds += Constants.InstantSingleDamageTalentReducesCastTimeOnOrbSpenderOnTargetPulsatingAoeInitialDelayReductionInSeconds;
        };
        // Tooltip: Haste to you while casting the Glacial Blast that consumes it: its cast time, and the hero's haste for
        // the cast. Combat logs: Bursting Ice pulses during such a cast come at more haste than before it
        // (GWChJ8P7tn6fHgV3 fight 22 @ 165.985-166.989; fight 20 @ 696.477, 805.680; fight 22 @ 427.146).
        hero.GetAbility<GlacialBlast>()?.CastHasteBonuses.Add(now => hero.Auras.HasAura(Buff, now) ? Constants.InstantSingleDamageTalentReducesCastTimeOnOrbSpenderAddedHasteWhileCastingOrbSpender : 0.0);
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (ability is GlacialBlast && hero.Auras.HasAura(Buff, now)) hero.Auras.Apply(CastingHaste, now);
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is GlacialBlast) hero.Auras.Remove(CastingHaste, now);
        };
        hero.OnCastCancelled += (ability, _, now) =>
        {
            if (ability is GlacialBlast) hero.Auras.Remove(CastingHaste, now);
        };
    }

    /// <summary>The haste of a Glacial Blast cast with Icy Flow, while it lasts (no aura of its own in the logs).</summary>
    public static readonly AuraDef CastingHaste = AuraDef.Internal("Icy Flow (casting)", 0, 1, StatModifier.Add(StatType.Haste, Constants.InstantSingleDamageTalentReducesCastTimeOnOrbSpenderAddedHasteWhileCastingOrbSpender));
}
