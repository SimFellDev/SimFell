using System.Reflection;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Logs.Replay;

namespace FellowshipSim.Benchmarks;

/// <summary>
/// Work done in one fight, counted through public events and read by reflection: decisions, aura changes, hits and events.
/// A stage's time per fight is its cost per op (the micro-benchmarks) times these counts.
/// </summary>
public static class Counters
{
    private static readonly FieldInfo Sequence = typeof(SimulationKernel).GetField("_sequence", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo DamageDealt = typeof(Hero).GetField("_damageDealt", BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static void Run()
    {
        foreach (bool idle in new[] { false, true })
        foreach (int targets in new[] { 1, 8 })
        {
            var hero = Fight.NewHero();
            var ctx = Fight.Start(hero, targets);
            var tally = new Tally(hero, ctx);
            var loop = new LoopCounts();
            Fight.RunTo(hero, ctx, Fight.Actions(idle), Fight.Duration, loop);
            tally.Print($"Monte Carlo, {Fight.Duration.TotalSeconds:F0} s, {targets} target(s){(idle ? ", the action list that idles" : "")}", loop);
        }

        if (FreeDiceBenchmarks.ScriptPath is { } path)
        {
            var script = ReplayScript.Load(path);
            var hero = ReplayDriver.CreateHero(script);
            var driver = new ReplayDriver(hero, script, new ReplayOptions { FreeDiceSeed = 1, StopOnFirstFailure = false, ForceCasts = true });
            var tally = new Tally(hero, driver.Context);
            driver.Run();
            tally.Print($"Free dice, {Path.GetFileName(path)} ({script.DurationSeconds:F0} s)", loop: null);
        }
        else
        {
            Console.WriteLine($"Free dice: {FreeDiceBenchmarks.ScriptVariable} is not set; skipped.");
        }
    }

    private static readonly FieldInfo Timers = typeof(AuraContainer).GetField("_timers", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Fights one after another on one hero, as a Monte Carlo run has them, each with its time and work: a fight that
    /// follows another should do the same work as the first.
    /// </summary>
    public static void FightSequence(int targets)
    {
        var hero = Fight.NewHero();
        var actions = Fight.Actions();
        int casts = 0, hits = 0;
        hero.OnCastCompleted += (_, _, _) => casts++;
        hero.OnDamageDealt += (in _) => hits++;
        Console.WriteLine($"Fights on one hero, {Fight.Duration.TotalSeconds:F0} s, {targets} target(s)");
        Console.WriteLine("  fight      ms        damage  casts   hits  events  hasted timers");
        // One thread: the simulator builds one hero, the one above.
        var simulator = new MonteCarloSimulator(() => hero, actions);
        for (int fight = 1; fight <= 12; fight++)
        {
            (casts, hits) = (0, 0);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var result = simulator.Run(Fight.Duration, iterations: 1, targetCount: targets, degreeOfParallelism: 1);
            clock.Stop();
            long events = (long)Sequence.GetValue(hero.SimContext!.Kernel)!;
            int timers = ((System.Collections.ICollection)Timers.GetValue(hero.Auras)!).Count;
            Console.WriteLine($"  {fight,5} {clock.Elapsed.TotalMilliseconds,7:F2} {result.TotalMeanDamage,13:N0} {casts,6} {hits,6} {events,7} {timers,14}");
        }
    }

    private sealed class Tally
    {
        private readonly Hero _hero;
        private readonly SimulationContext _ctx;
        private int _heroGains, _heroRemovals, _hasteOrRecoveryChanges, _enemyChanges, _hits, _casts;
        private readonly Dictionary<string, int> _castsByAbility = new();

        public Tally(Hero hero, SimulationContext ctx)
        {
            _hero = hero;
            _ctx = ctx;
            var ownRecovery = hero.Abilities.SelectMany(a => a.OwnRecoveryAuraLogIds).ToHashSet();
            bool TouchesCooldowns(Aura aura) =>
                ownRecovery.Contains(aura.LogId) ||
                aura.Modifiers.Any(m => m.Type is StatType.Haste or StatType.HasteRating or StatType.CooldownRecovery);
            hero.Auras.OnGained += (aura, _, _) =>
            {
                _heroGains++;
                if (TouchesCooldowns(aura)) _hasteOrRecoveryChanges++;
            };
            hero.Auras.OnRemoved += (aura, _) =>
            {
                _heroRemovals++;
                if (TouchesCooldowns(aura)) _hasteOrRecoveryChanges++;
            };
            foreach (var enemy in ctx.AllEnemies)
            {
                enemy.Auras.OnGained += (_, _, _) => _enemyChanges++;
                enemy.Auras.OnRemoved += (_, _) => _enemyChanges++;
            }
            hero.OnDamageDealt += (in _) => _hits++;
            hero.OnCastCompleted += (ability, _, _) =>
            {
                _casts++;
                _castsByAbility[ability.Name] = _castsByAbility.GetValueOrDefault(ability.Name) + 1;
            };
        }

        public void Print(string title, LoopCounts? loop)
        {
            long events = (long)Sequence.GetValue(_ctx.Kernel)!;
            int queued = _ctx.Kernel.Count;
            // The tally's own handler is not counted.
            int subscribers = ((System.Collections.ICollection)DamageDealt.GetValue(_hero)!).Count - 1;
            Console.WriteLine(title);
            Console.WriteLine($"  total damage            {_ctx.TotalDamage:N0}");
            Console.WriteLine($"  casts                   {_casts}  ({string.Join(", ", _castsByAbility.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))})");
            if (loop != null)
            {
                Console.WriteLine($"  decision-loop passes    {loop.Passes}");
                Console.WriteLine($"  action-list decisions   {loop.Decisions}  (cast nothing: {loop.EmptyDecisions})");
                Console.WriteLine($"  idle wake-ups           {loop.IdleWakes}");
            }
            Console.WriteLine($"  hero aura gains         {_heroGains}  removals {_heroRemovals}  (touching haste or recovery: {_hasteOrRecoveryChanges})");
            if (loop != null) Console.WriteLine($"  enemy aura changes      {_enemyChanges}");
            Console.WriteLine($"  cooldown states         {_hero.Cooldowns.Count}  (advances on hero aura changes: {(long)(_heroGains + _heroRemovals) * _hero.Cooldowns.Count})");
            Console.WriteLine($"  hits                    {_hits}  (OnDamageDealt and Ability.Hit subscribers: {subscribers})");
            Console.WriteLine($"  events scheduled        {events}  (left in queue at the end: {queued})");
        }
    }
}
