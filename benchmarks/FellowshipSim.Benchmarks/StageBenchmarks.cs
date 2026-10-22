using BenchmarkDotNet.Attributes;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Benchmarks;

/// <summary>
/// Stage A: one full evaluation of the action list on a hero 60 s into a fight: every action's target selection,
/// condition and CanCast, as one decision runs them when nothing is cast.
/// </summary>
[MemoryDiagnoser]
public class AplDecisionBenchmarks
{
    private Hero _hero = null!;
    private SimulationContext _ctx = null!;
    private IReadOnlyList<BoundAplStep> _actions = null!;

    [Params(1, 8)]
    public int Targets { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var (hero, ctx, actions) = Fight.AtMidFight(Targets);
        (_hero, _ctx, _actions) = (hero, ctx, actions.Bind(hero).Actions);
    }

    [Benchmark]
    public int EvaluateActionList()
    {
        int ready = 0;
        foreach (var action in _actions)
        {
            if (action is BoundAplAction ability ? ability.CanExecute(_ctx) : action.ConditionHolds(_ctx)) ready++;
        }
        return ready;
    }
}

/// <summary>
/// Stage B: one aura applied and removed on a hero 60 s into a fight, with a stat read after each change, so each op
/// includes the cooldown advance on every aura change and the stat folds rebuilt. The auras have no haste, so the hero's
/// state is the same after each op.
/// </summary>
[MemoryDiagnoser]
public class AuraBenchmarks
{
    private static readonly AuraDef CritAura = AuraDef.Internal("Benchmark Crit", 10, 1, StatModifier.Add(StatType.CritChance, 0.05));
    private static readonly AuraDef NoStatAura = AuraDef.Internal("Benchmark Marker", 10);

    private Hero _hero = null!;
    private SimulationContext _ctx = null!;
    private Hero _noCooldowns = null!;
    private SimulationContext _noCooldownsCtx = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        (_hero, _ctx, _) = Fight.AtMidFight(1);
        // The same hero with no cooldown states: an aura change advances no cooldown.
        (_noCooldowns, _noCooldownsCtx, _) = Fight.AtMidFight(1);
        _noCooldowns.Cooldowns.Clear();
    }

    [Benchmark]
    public double ApplyRemoveCritAura() => ApplyRemove(_hero, _ctx, CritAura);

    [Benchmark]
    public double ApplyRemoveNoStatAura() => ApplyRemove(_hero, _ctx, NoStatAura);

    /// <summary><see cref="ApplyRemoveNoStatAura"/> with no cooldowns; the difference is the cooldown advance of two changes.</summary>
    [Benchmark]
    public double ApplyRemoveNoStatAuraNoCooldowns() => ApplyRemove(_noCooldowns, _noCooldownsCtx, NoStatAura);

    public static double ApplyRemove(Hero hero, SimulationContext ctx, AuraDef def)
    {
        var now = ctx.CurrentTime;
        hero.Auras.Apply(def, now);
        double during = hero.GetHaste(now) + hero.CritChance(now);
        hero.Auras.Remove(def, now);
        return during + hero.GetHaste(now) + hero.CritChance(now);
    }
}

/// <summary>
/// Stage B with haste: as <see cref="AuraBenchmarks"/>, with every hasted timer updated on each change. An update
/// reschedules the timer and leaves its old event cancelled in the queue, so each iteration starts from a new hero.
/// </summary>
[MemoryDiagnoser]
public class HasteAuraBenchmarks
{
    private const int ChangesPerIteration = 2048;
    private static readonly AuraDef HasteAura = AuraDef.Internal("Benchmark Haste", 10, 1, StatModifier.Add(StatType.Haste, 0.1));

    private Hero _hero = null!;
    private SimulationContext _ctx = null!;

    [IterationSetup]
    public void IterationSetup() => (_hero, _ctx, _) = Fight.AtMidFight(1);

    [Benchmark]
    [InvocationCount(ChangesPerIteration)]
    public double ApplyRemoveHasteAura() => AuraBenchmarks.ApplyRemove(_hero, _ctx, HasteAura);
}

/// <summary>
/// Stage C: one hit on a hero 60 s into a fight. <see cref="FrostBoltDealDamage"/> is the full path (base damage from
/// stats, crit, the hit closures, OnDamageDealt to every subscriber); <see cref="ApplyDamageFixedNumbers"/> skips the stat
/// reads; <see cref="DamageDealtFanOut"/> is the OnDamageDealt fan-out alone, and <see cref="DamageDealtFanOutNoTarget"/>
/// its floor. Hits trigger procs, so each iteration starts from a new hero.
/// </summary>
[MemoryDiagnoser]
public class HitBenchmarks
{
    private const int HitsPerIteration = 2048;

    private Hero _hero = null!;
    private SimulationContext _ctx = null!;
    private Ability _frostBolt = null!;
    private EnemyTarget _target = null!;

    [Params(1, 8)]
    public int Targets { get; set; }

    [IterationSetup]
    public void IterationSetup()
    {
        (_hero, _ctx, _) = Fight.AtMidFight(Targets);
        _frostBolt = _hero.GetAbility("Frost Bolt")!;
        _target = _ctx.AllEnemies[0];
    }

    [Benchmark]
    [InvocationCount(HitsPerIteration)]
    public double FrostBoltDealDamage() => _frostBolt.DealDamage(_ctx, _target);

    [Benchmark]
    [InvocationCount(HitsPerIteration)]
    public double ApplyDamageFixedNumbers() =>
        _ctx.ApplyDamage(_hero, _target, 1000.0, _frostBolt.DamageLogId, _frostBolt.Name, canCrit: true, critChance: 0.3, critMultiplier: 2.0);

    [Benchmark]
    [InvocationCount(HitsPerIteration)]
    public void DamageDealtFanOut() =>
        _hero.TriggerDamageDealt(new DamageContext(_hero, _target, _frostBolt.DamageLogId, _frostBolt.Name, 1000.0, false, _ctx.CurrentTime, _ctx));

    /// <summary>The fan-out of a hit with no target and no damage: most subscribers return at their first check.</summary>
    [Benchmark]
    [InvocationCount(HitsPerIteration)]
    public void DamageDealtFanOutNoTarget() =>
        _hero.TriggerDamageDealt(new DamageContext(_hero, null, _frostBolt.DamageLogId, _frostBolt.Name, 0.0, false, _ctx.CurrentTime, _ctx));
}

/// <summary>
/// Stage D: the event queue. <see cref="ScheduleCancelDequeue"/> schedules 128 events (one in four cancelled) and fires
/// the rest, per event; <see cref="PeriodicEffectBeginStop"/> starts a 10 s effect ticking every second (all ticks
/// scheduled at once) and stops it.
/// </summary>
[MemoryDiagnoser]
public class EventQueueBenchmarks
{
    private const int Events = 128;
    private static readonly Action<SimulationContext> NoOp = static _ => { };

    private SimulationKernel _kernel = null!;
    private SimulationContext _ctx = null!;
    private readonly SimTime[] _times = new SimTime[Events];
    private int _fired;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _kernel = new SimulationKernel();
        // A context with no fight running: only the benchmark's events are queued.
        _ctx = new SimulationContext(Fight.NewHero());
        var rng = new Random(7);
        for (int i = 0; i < Events; i++) _times[i] = SimTime.FromSeconds(rng.NextDouble() * 30.0);
    }

    [Benchmark(OperationsPerInvoke = Events)]
    public int ScheduleCancelDequeue()
    {
        for (int i = 0; i < Events; i++)
        {
            var ev = _kernel.ScheduleAt(_times[i], NoOp, "Benchmark");
            if ((i & 3) == 0) ev.Cancel();
        }
        int fired = 0;
        while (_kernel.TryDequeueDue(SimTime.Infinite, out _)) fired++;
        return fired;
    }

    [Benchmark(OperationsPerInvoke = Events)]
    public int ScheduleCapturingClosure()
    {
        for (int i = 0; i < Events; i++)
        {
            int index = i;
            _kernel.ScheduleAt(_times[i], _ => _fired += index, "Benchmark");
        }
        int fired = 0;
        while (_kernel.TryDequeueDue(SimTime.Infinite, out _)) fired++;
        return fired + _fired;
    }

    [Benchmark]
    public SimTime? PeriodicEffectBeginStop()
    {
        var effect = PeriodicEffect.Begin(_ctx, "Benchmark", 10.0, 1.0, tickOnStart: false, partialFinalTick: true,
            onTick: static (_, _) => { });
        effect.Stop(_ctx);
        // The cancelled ticks leave the queue as they reach its front.
        return _ctx.Kernel.PeekNextTime();
    }
}
