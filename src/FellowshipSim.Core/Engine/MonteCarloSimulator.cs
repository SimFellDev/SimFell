using FellowshipSim.Core.APL;

namespace FellowshipSim.Core.Engine;

public class MonteCarloResult
{
    public double MeanDps { get; init; }
    public double MinDps { get; init; }
    public double MaxDps { get; init; }
    public double StandardDeviation { get; init; }
    public double TotalMeanDamage { get; init; }
    public Dictionary<string, double> AbilityDpsBreakdown { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Total damage of each fight, in seed order.</summary>
    public IReadOnlyList<double> IterationDamage { get; init; } = [];
    public int Iterations { get; init; }
    public double DurationSeconds { get; init; }
}

public class MonteCarloSimulator
{
    private readonly Hero _hero;
    private readonly AplProgram _program;

    /// <summary>A simulator of <paramref name="hero"/> casting by <paramref name="actions"/>; an action naming no ability of the hero throws.</summary>
    public MonteCarloSimulator(Hero hero, AplActionLists actions)
    {
        _hero = hero;
        _program = actions.Bind(hero);
    }

    /// <summary>Runs <paramref name="iterations"/> fights. Fight i uses seed <paramref name="firstSeed"/> + i, so a run can be repeated exactly.</summary>
    public MonteCarloResult Run(SimTime duration, int iterations = 100, int targetCount = 1, int firstSeed = 1)
    {
        double totalSeconds = Math.Max(1.0, duration.TotalSeconds);
        var iterationDps = new List<double>(iterations);
        var iterationDamage = new List<double>(iterations);
        var abilityDamageAggregator = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        for (int iter = 0; iter < iterations; iter++)
        {
            var ctx = new SimulationContext(_hero);
            ctx.Rng.Seed(firstSeed + iter);
            _hero.ResetForIteration();
            _hero.BeginFight(ctx);

            // Populate enemies
            for (int i = 1; i <= targetCount; i++)
            {
                ctx.AddEnemy(100 + i, i, $"Training Mob {i}", maxHealth: 100_000_000);
            }
            ctx.Player.CurrentTarget = ctx.AllEnemies.First();

            // Main loop: whenever the hero is free to act, run the first ready APL action, then let the
            // event queue carry time forward to the next decision point.
            while (ctx.CurrentTime < duration)
            {
                bool acted = IsFreeToAct(ctx) && _program.TryAct(ctx);

                var next = NextDecisionTime(ctx);
                if (!acted && next <= ctx.CurrentTime)
                {
                    next = ctx.CurrentTime + SimTime.FromSeconds(0.1);
                }
                ctx.RunUntil(next < duration ? next : duration);
            }

            _hero.Auras.FinalizeUptimes(duration);

            double iterDps = ctx.TotalDamage / totalSeconds;
            iterationDps.Add(iterDps);
            iterationDamage.Add(ctx.TotalDamage);

            foreach (var (ability, dmg) in ctx.AbilityDamage)
            {
                abilityDamageAggregator.TryGetValue(ability, out double cur);
                abilityDamageAggregator[ability] = cur + dmg;
            }
        }

        double meanDps = iterationDps.Average();
        double minDps = iterationDps.Min();
        double maxDps = iterationDps.Max();

        double sumSquares = iterationDps.Sum(d => Math.Pow(d - meanDps, 2));
        double stdDev = Math.Sqrt(sumSquares / iterationDps.Count);

        var abilityDps = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (ability, totalDmg) in abilityDamageAggregator)
        {
            abilityDps[ability] = (totalDmg / iterations) / totalSeconds;
        }

        return new MonteCarloResult
        {
            MeanDps = meanDps,
            MinDps = minDps,
            MaxDps = maxDps,
            StandardDeviation = stdDev,
            TotalMeanDamage = meanDps * totalSeconds,
            AbilityDpsBreakdown = abilityDps,
            IterationDamage = iterationDamage,
            Iterations = iterations,
            DurationSeconds = totalSeconds
        };
    }

    private bool IsFreeToAct(SimulationContext ctx) =>
        _hero.CurrentCast == null && _hero.CurrentChannel == null && ctx.CurrentTime >= _hero.GlobalCooldownEndTime;

    private SimTime NextDecisionTime(SimulationContext ctx)
    {
        var next = _hero.GlobalCooldownEndTime;
        if (_hero.CurrentCast != null && _hero.CurrentCast.EndTime > next) next = _hero.CurrentCast.EndTime;
        if (_hero.CurrentChannel != null && _hero.CurrentChannel.Ticks.End > next) next = _hero.CurrentChannel.Ticks.End;
        return next;
    }
}
