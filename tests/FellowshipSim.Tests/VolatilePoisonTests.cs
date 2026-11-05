using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Volatile Poison has a rolled duration and ticks a sixth of it apart, shortened by Haste. A new application rolls again, and
/// the poison erupts as it runs out.
/// </summary>
public class VolatilePoisonTests
{
    private const double Haste = 0.2;
    private const double EruptionDelay = 0.1;

    [Fact]
    public void A_Tick_Comes_Every_Sixth_Of_The_Rolled_Duration_Shortened_By_Haste()
    {
        var run = new Run(7.8);

        run.Apply();
        run.ToSeconds(9.0);

        double period = 7.8 / VolatilePoison.BaseTicks / (1.0 + Haste);
        var ticks = run.Ticks();
        Assert.Equal(8, ticks.Count);
        for (int k = 0; k < 7; k++) Assert.Equal((k + 1) * period, ticks[k].Time, 0.01);
    }

    [Fact]
    public void The_Last_Tick_Is_The_Period_Run_When_The_Duration_Runs_Out()
    {
        var run = new Run(7.8);

        run.Apply();
        run.ToSeconds(9.0);

        var ticks = run.Ticks();
        double frameOfPeriod = PeriodicEffect.ServerFrameSeconds * (1.0 + Haste) * VolatilePoison.BaseTicks / 7.8;
        Assert.InRange(ticks[^1].Damage / ticks[0].Damage, 0.2, 0.2 + (2 * frameOfPeriod));
    }

    [Fact]
    public void The_Poison_Erupts_After_Its_Delay_When_Its_Duration_Runs_Out()
    {
        var run = new Run(7.8);

        run.Apply();
        run.ToSeconds(9.0);

        Assert.Equal(7.8 + EruptionDelay, Assert.Single(run.Eruptions()).Time, 0.05);
    }

    [Fact]
    public void A_Simulation_Rolls_The_Eruption_Delay_Up_To_Its_Longest()
    {
        var run = new Run();
        var dice = new RandomOutcomeSource(new SimulationRng(7));

        var delays = Enumerable.Range(0, 100).Select(_ => dice.RollDelay(VolatilePoison.EruptionMaxDelaySeconds, run.Context)).ToList();

        Assert.All(delays, delay => Assert.InRange(delay, 0.0, VolatilePoison.EruptionMaxDelaySeconds));
        Assert.True(delays.Distinct().Count() > 1);
    }

    [Fact]
    public void A_New_Application_Rolls_Its_Duration_And_Scales_The_Time_To_The_Next_Tick()
    {
        var run = new Run(6.0, 8.0);
        double period = 6.0 / VolatilePoison.BaseTicks / (1.0 + Haste);
        double refresh = 1.5 * period;

        run.Apply();
        run.ToSeconds(refresh);
        run.Apply();
        run.ToSeconds(12.0);

        double longer = 8.0 / 6.0;
        var ticks = run.Ticks();
        Assert.Equal(period, ticks[0].Time, 0.01);
        Assert.Equal(refresh + (0.5 * period * longer), ticks[1].Time, 0.01);
        Assert.Equal(ticks[1].Time + (period * longer), ticks[2].Time, 0.01);
        var eruption = Assert.Single(run.Eruptions());
        Assert.Equal(refresh + 8.0 + EruptionDelay, eruption.Time, 0.05);
    }

    [Fact]
    public void An_Enemy_Leaving_The_Fight_Takes_The_Poison_With_No_Eruption()
    {
        var run = new Run(7.8);

        run.Apply();
        run.ToSeconds(3.0);
        run.Context.RemoveEnemy(run.Enemy);
        run.ToSeconds(9.0);

        Assert.Empty(run.Eruptions());
        Assert.All(run.Ticks(), tick => Assert.True(tick.Time <= 3.0));
    }

    /// <summary>One poison on one enemy at 20% Haste, with the rolled durations given, in order.</summary>
    private sealed class Run
    {
        private readonly Mara _hero;
        private readonly Hits _hits;

        public Run(params double[] durations)
        {
            _hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
            Context = new SimulationContext(_hero);
            _hero.ResetForIteration();
            _hero.BeginFight(Context);
            Enemy = Context.AddEnemy(100, 1, "Dummy", 1e12);
            _hero.CurrentTarget = Enemy;
            _hits = new Hits(Context.Outcomes, durations);
            Context.Outcomes = _hits;
            double baseHaste = _hero.GetHaste(Context.CurrentTime);
            _hero.Auras.Apply(AuraDef.Effect("Test Haste", 999_002, modifiers: StatModifier.Add(StatType.Haste, Haste - baseHaste)), Context.CurrentTime);
        }

        public SimulationContext Context { get; }

        public EnemyTarget Enemy { get; }

        public void Apply() => _hero.VolatilePoison.ApplyTo(Context, Enemy);

        public void ToSeconds(double seconds) => Context.RunUntil(SimTime.FromSeconds(seconds));

        public List<(double Time, double Damage)> Ticks() => _hits.OfId(LogIds.Effect(VolatilePoison.EffectId));

        public List<(double Time, double Damage)> Eruptions() => _hits.OfId(LogIds.Effect(VolatilePoison.EruptionEffectId));
    }

    /// <summary>Gives the durations, and keeps each hit's time and base damage as it lands.</summary>
    private sealed class Hits(IOutcomeSource inner, double[] durations) : IOutcomeSource
    {
        private readonly List<(int Id, double Time, double Damage)> _hits = new();
        private int _rolls;

        public List<(double Time, double Damage)> OfId(int id) =>
            _hits.Where(h => h.Id == id).Select(h => (h.Time, h.Damage)).ToList();

        public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null) => inner.RollProc(procKey, chance, ctx, target);
        public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) => inner.SelectTargets(damageLogId, candidates, ctx);
        public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) => inner.ChooseTarget(damageLogId, candidates, ctx, preferred);
        public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => inner.ResolveHit(hit, ctx);
        public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx)
        {
            _hits.Add((landed.AbilityId, ctx.CurrentTime.TotalSeconds, landed.BaseDamage));
            return inner.Land(landed, outcome, ctx);
        }
        public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => inner.TickAt(tick, ctx);
        public double RollDuration(in EffectDurationRequest application, SimulationContext ctx) => durations[_rolls++];
        public double RollDelay(double maxSeconds, SimulationContext ctx) => EruptionDelay;
        public void HitFinished(SimulationContext ctx) => inner.HitFinished(ctx);
        public SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => inner.ReactionDelay(hold, reactions, ctx);
    }
}
