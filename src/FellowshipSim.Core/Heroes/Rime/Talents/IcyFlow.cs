using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 44 / effect 2308 (15s, 2 stacks): Cold Snap grants a stack; the next Glacial Blast is cast with +50% Haste, the
/// next Ice Comet lands 0.5s sooner, and the one consuming it gets +20% crit chance (talent text and hero_data
/// "ReducesCastTimeOnOrbSpender" AddedCritChance 0.2; the buff's own text says +25%). Talon Strike and Rising Talons
/// consume it too (Icy Talons). Combat logs (GWChJ8P7tn6fHgV3 fights 20 and 22) have the haste. UNVERIFIED: the crit
/// bonus (+20% or +25%); Ice Comet's impact delay is not modeled.
/// </summary>
public class IcyFlow : Talent
{
    public const int TalentId = 44;
    public const string TalentName = "Icy Flow";
    public const int EffectId = 2308;
    public const double BuffSeconds = 15.0;
    public const int MaxStacks = 2;
    public const double CritChance = 0.2;
    /// <summary>
    /// Effect 3119 (game-data-authority, GA_Rime_CastedProjectileDamageOrbSpender 3957 and 4004): Haste Additive, row
    /// Rime.InstantSingleDamage.Talent.ReducesCastTimeOnOrbSpender.AddedHasteWhileCastingOrbSpender, on the hero from the
    /// Glacial Blast's activation with an Icy Flow stack to the ability's end.
    /// </summary>
    public const double CastHaste = 0.5;
    /// <summary>"causing your next Ice Comet to have 0.5 seconds reduced impact delay" (hero_data InitialDelayReductionInSeconds).</summary>
    public const double ImpactDelayReductionSeconds = 0.5;

    public static readonly AuraDef Buff = AuraDef.Effect("Icy Flow", EffectId, BuffSeconds, MaxStacks);

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
            cast.CritChance += CritChance;
            if (cast.Ability is IceComet) cast.DelayReductionSeconds += ImpactDelayReductionSeconds;
        };
        // "+50% Haste to you while casting" the Glacial Blast that consumes it: its cast time, and the hero's haste for
        // the cast. Combat logs: Bursting Ice pulses during such a cast come at 0.48-0.54 more haste than before it
        // (GWChJ8P7tn6fHgV3 fight 22 @ 165.985-166.989: 0.334s to 0.245s; fight 20 @ 696.477, 805.680; fight 22 @ 427.146).
        hero.GetAbility<GlacialBlast>()?.CastHasteBonuses.Add(now => hero.Auras.HasAura(Buff, now) ? CastHaste : 0.0);
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
    public static readonly AuraDef CastingHaste = AuraDef.Internal("Icy Flow (casting)", 0, 1, StatModifier.Add(StatType.Haste, CastHaste));
}
