using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Read the hero's events in a fight, by kind and game name, in the order they happened: <c>f.Damage("Frost Bolt")</c>,
/// <c>f.Gains("Anima")</c>, <c>f.Spends("Winter Orbs")</c>... Each returns a list, so <c>.Count</c>, <c>.Single()</c> and
/// <c>.First().Time</c> work directly. Tests use these instead of filtering <see cref="StepDriver.Events"/>.
///
/// Use them for what state cannot show: when something happened, in what order, and each hit's value. <c>Cast</c> returns only when the
/// hero is free, so the middle of a cast is over when the test continues; the events keep it (Anima gained at the end of the cast, not the
/// start). For current state (resources, buffs, cooldowns), use <see cref="StepVerbs"/>.
///
/// Casts and resources are looked up in the hero's abilities and resources, and a name the hero does not have throws. Auras are passed by
/// definition (<c>BurstingIce.WintersEmbrace</c>), so a typo does not compile. Hits are the exception: the engine has no list of hit names,
/// so <see cref="Damage(StepDriver, string)"/> returns an empty list for a misspelled name.
/// </summary>
public static class StepQueries
{
    /// <summary>
    /// The damage events named <paramref name="hit"/>: an ability's hits, or a talent's, an aspect's...
    ///
    /// Unlike the other queries, a name the hero cannot have does not throw: the engine keeps no list of hit names to check it
    /// against, so a typo returns an empty list, as if nothing hit. A test that expects 0 hits of a name also sees it hit in
    /// another case (WRITING_TESTS.md, Asserting).
    /// </summary>
    public static List<StepEvent> Damage(this StepDriver f, string hit) => f.EventsOf(StepEventKind.Damage, hit);

    /// <summary>The damage events of the hits named after <typeparamref name="T"/>.</summary>
    public static List<StepEvent> Damage<T>(this StepDriver f) where T : Ability => f.Damage(f.Ability<T>().Name);

    /// <summary>The value of the first hit named <paramref name="hit"/> on each enemy, one per enemy hit, in the order they were first hit: to
    /// compare a hit on a crowd with the same hit on one enemy (area damage reduction).</summary>
    public static List<double> FirstHitOnEachEnemy(this StepDriver f, string hit) =>
        f.Damage(hit).GroupBy(h => h.Target).Select(g => g.First().Value).ToList();

    /// <summary>Gains of the resource named <paramref name="resource"/> ("Anima", "Winter Orbs", "Spirit"...), one event per gain.</summary>
    public static List<StepEvent> Gains(this StepDriver f, string resource) =>
        f.EventsOf(StepEventKind.ResourceGained, f.Resource(resource).Name);

    public static List<StepEvent> Spends(this StepDriver f, string resource) =>
        f.EventsOf(StepEventKind.ResourceSpent, f.Resource(resource).Name);

    /// <summary>Gains of the resource that were lost because it was full.</summary>
    public static List<StepEvent> Overflows(this StepDriver f, string resource) =>
        f.EventsOf(StepEventKind.ResourceOverflow, f.Resource(resource).Name);

    public static List<StepEvent> CastsStarted(this StepDriver f, string ability) =>
        f.EventsOf(StepEventKind.CastStarted, f.Ability(ability).Name);

    public static List<StepEvent> CastsCompleted(this StepDriver f, string ability) =>
        f.EventsOf(StepEventKind.CastCompleted, f.Ability(ability).Name);

    public static List<StepEvent> AurasRemoved(this StepDriver f, AuraDef aura) => f.EventsOf(StepEventKind.AuraRemoved, aura.Name);

    /// <summary>When the last cast of <paramref name="ability"/> completed, in seconds.</summary>
    public static double CompletedAt(this StepDriver f, string ability) => f.CastsCompleted(ability).Last().Time.TotalSeconds;

    /// <summary>How long the last cast of <paramref name="ability"/> took from start to completion, in seconds, without the global cooldown after
    /// it. 0 for a channel, whose cast completes as the channel starts.</summary>
    public static double LastCastDuration(this StepDriver f, string ability) =>
        (f.CastsCompleted(ability).Last().Time - f.CastsStarted(ability).Last().Time).TotalSeconds;

    private static List<StepEvent> EventsOf(this StepDriver f, StepEventKind kind, string name) =>
        f.Events.Where(e => e.Kind == kind && e.Name == name).ToList();
}
