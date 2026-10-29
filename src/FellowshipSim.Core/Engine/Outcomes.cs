using System.Numerics;

namespace FellowshipSim.Core.Engine;

/// <summary>Everything the damage pipeline knows about a hit before its random parts are decided.</summary>
public readonly record struct HitRequest(
    Hero Source,
    EnemyTarget? Target,
    int AbilityId,
    string AbilityName,
    double BaseDamage,
    bool CanCrit,
    double CritChance,
    double CritMultiplier,
    SimTime Time,
    double TickFraction = 1.0,
    bool? ForcedCrit = null);

/// <summary>A tick of a periodic effect with a damage log ID, at the time the engine schedules it.</summary>
/// <param name="Fraction">The tick's fraction of a full tick (below 1 for a partial final tick).</param>
public readonly record struct TickRequest(
    Hero Source,
    EnemyTarget? Target,
    int DamageLogId,
    string Name,
    SimTime Scheduled,
    double Fraction);

/// <summary>
/// What is decided when a hit is released: whether it crits, how long it travels before landing, and a damage
/// spread roll. Damage itself is computed when the hit lands, from the stats at that moment.
/// </summary>
/// <param name="Missed">The hit never lands (e.g. its target died while it travelled): no damage, nothing triggers.</param>
public readonly record struct HitOutcome(bool IsCrit, SimTime LandingDelay = default, double Spread = 1.0, object? Token = null, bool Missed = false);

/// <summary>
/// Decides every random outcome in the simulation: proc rolls, crits and damage spread.
/// Game code asks this instead of rolling dice itself, so the same mechanics can run against
/// random rolls or outcomes supplied from elsewhere.
/// </summary>
public interface IOutcomeSource
{
    /// <summary>
    /// Rolls a named proc. <paramref name="procKey"/> identifies the proc, e.g. "inspired_allegiance";
    /// <paramref name="target"/> is the enemy its effect lands on, if any.
    /// </summary>
    bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null);

    /// <summary>
    /// Which of <paramref name="candidates"/> a multi-target hit with damage ID <paramref name="damageLogId"/> strikes
    /// (enemies in its area). Simulations hit every candidate; a replay answers from the log.
    /// </summary>
    IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx);

    /// <summary>
    /// The enemy a hit that picks its own target (e.g. a summoned attacker) strikes, out of <paramref name="candidates"/>.
    /// Simulations pick at random; a replay answers from the log.
    /// </summary>
    /// <remarks>
    /// With <paramref name="preferred"/> the hit normally goes there (e.g. "attacks your target"); a replay still
    /// follows the log when the log shows it elsewhere.
    /// </remarks>
    EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null);

    /// <summary>Decides a hit's crit, travel time and spread when it is released.</summary>
    HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx);

    /// <summary>
    /// The hit lands: returns the damage it deals. <paramref name="landed"/> has the base damage and crit values computed
    /// at landing.
    /// </summary>
    double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx);

    /// <summary>Firing time of a periodic tick with a damage log ID.</summary>
    SimTime TickAt(in TickRequest tick, SimulationContext ctx);

    /// <summary>Called once a landed hit has been dealt and everything it triggered (procs, follow-up hits) has run.</summary>
    void HitFinished(SimulationContext ctx);
}

/// <summary>
/// Rolls every outcome: crits, procs and damage spread (uniform within +/-10%). Each crit is a real one, so effects that
/// trigger on crits (Amethyst Splinters, Pyrophibian Frenzy) trigger as often as the crit chance says.
/// </summary>
public class RandomOutcomeSource : IOutcomeSource
{
    // Game damage variance is +/- 10% (uniform random in [0.90, 1.10]).
    public const double DamageSpread = 0.10;

    private readonly SimulationRng _rng;

    public RandomOutcomeSource(SimulationRng rng)
    {
        _rng = rng;
    }

    public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null) => _rng.Roll(chance);

    public virtual IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) => candidates;

    public virtual EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) =>
        preferred ?? (candidates.Count == 0 ? null : candidates[(int)(_rng.NextDouble() * candidates.Count) % candidates.Count]);

    public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx)
    {
        // A hit at an enemy that has left combat (its dungeon segment ended) misses, and rolls nothing.
        if (hit.Target is { IsSleeping: true }) return new HitOutcome(false, Missed: true);
        // A forced crit rolls nothing: no crit and no spread.
        if (hit.ForcedCrit is { } forced) return new HitOutcome(forced);
        bool isCrit = hit.CanCrit && _rng.Roll(hit.CritChance);
        double spread = (1.0 - DamageSpread) + (_rng.NextDouble() * 2.0 * DamageSpread);
        return new HitOutcome(isCrit, SimTime.Zero, spread);
    }

    public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx)
    {
        if (landed.ForcedCrit.HasValue) return landed.BaseDamage * (outcome.IsCrit ? landed.CritMultiplier : 1.0);
        return landed.BaseDamage * ((outcome.IsCrit ? landed.CritMultiplier : 1.0) * outcome.Spread);
    }

    public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => tick.Scheduled;

    public void HitFinished(SimulationContext ctx) { }
}

/// <summary>The simulation's random numbers: xoshiro256** (Blackman and Vigna, 2018), seeded through SplitMix64.</summary>
public sealed class SimulationRng
{
    private ulong _s0, _s1, _s2, _s3;

    public SimulationRng(int seed = 42)
    {
        Seed(seed);
    }

    public void Seed(int seed)
    {
        // SplitMix64 spreads the seed over the four state words, so the state is never all zero.
        ulong x = (ulong)seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    public bool Roll(double chance) => NextDouble() < chance;

    /// <summary>Uniform in [0, 1): the top 53 bits of the next number.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    private ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        ulong z = x += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
