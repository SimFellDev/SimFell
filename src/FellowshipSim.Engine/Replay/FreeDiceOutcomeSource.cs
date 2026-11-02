namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Free dice for a replay: the engine rolls its own crits, procs and damage spread, but where everyone stands comes from
/// the log. Hits that strike several enemies, or pick their own target, only reach the enemies the player was fighting:
/// those the log shows the player hitting (with anything) between the latest cast and the next one.
/// </summary>
public sealed class FreeDiceOutcomeSource : RandomOutcomeSource
{
    private readonly SimulationRng _rng;
    private readonly double[] _castTimes;
    private readonly HashSet<(int Id, int Instance)>[] _engaged;

    /// <summary>The script's cast times and the enemies engaged in each window between them. Runs only read it.</summary>
    private sealed record Engagement(double[] CastTimes, HashSet<(int Id, int Instance)>[] Engaged);

    // Built once per script: free dice runs one script many times, on several threads.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReplayScript, Engagement> Engagements = new();

    public FreeDiceOutcomeSource(ReplayScript script, SimulationRng rng) : base(rng)
    {
        _rng = rng;
        (_castTimes, _engaged) = Engagements.GetValue(script, BuildEngagement);
        _hitsByKey = HitsByKey.GetValue(script, s => s.Observations.Damage.OrderBy(d => d.Time)
            .GroupBy(d => (d.AbilityId, d.Target)).ToDictionary(g => g.Key, g => g.ToList()));
    }

    private static Engagement BuildEngagement(ReplayScript script)
    {
        var castTimes = script.Commands.Where(c => c.IsPlayerAction).Select(c => c.Time).Distinct().Order().ToArray();
        // Window i runs from cast i to cast i + 1; window 0 also covers anything before the first cast.
        var engaged = new HashSet<(int Id, int Instance)>[Math.Max(1, castTimes.Length)];
        for (int i = 0; i < engaged.Length; i++) engaged[i] = new HashSet<(int Id, int Instance)>();
        foreach (var hit in script.Observations.Damage)
        {
            engaged[Window(castTimes, engaged.Length, hit.Time)].Add((hit.Target.Id, hit.Target.Instance));
        }
        return new Engagement(castTimes, engaged);
    }

    private int Window(double time) => Window(_castTimes, _engaged.Length, time);

    private static int Window(double[] castTimes, int windows, double time)
    {
        int i = Array.BinarySearch(castTimes, time);
        if (i < 0) i = ~i - 1;
        return Math.Clamp(i, 0, windows - 1);
    }

    private List<EnemyTarget> Engaged(IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx)
    {
        var engaged = _engaged[Window(ctx.CurrentTime.TotalSeconds)];
        var list = new List<EnemyTarget>(Math.Min(candidates.Count, engaged.Count));
        for (int i = 0; i < candidates.Count; i++)
        {
            if (engaged.Contains((candidates[i].Id, candidates[i].InstanceId))) list.Add(candidates[i]);
        }
        return list;
    }

    public override IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) =>
        Engaged(candidates, ctx);

    public override EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null)
    {
        if (preferred != null) return preferred;
        var engaged = Engaged(candidates, ctx);
        return engaged.Count == 0 ? null : engaged[(int)(_rng.NextDouble() * engaged.Count) % engaged.Count];
    }

    /// <summary>How far from now a defensive proc's log hit may be (a defensive proc fires at its log proof).</summary>
    public const double LoggedHitWindowSeconds = 0.1;

    // The script's hits by damage ID and enemy, in time order, built once per script.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReplayScript, Dictionary<(int Id, UnitRef Target), List<LogDamage>>> HitsByKey = new();
    private readonly Dictionary<(int Id, UnitRef Target), List<LogDamage>> _hitsByKey;
    private readonly HashSet<LogDamage> _taken = new();

    /// <summary>The amount of the nearest log hit with the ID on the enemy, untaken, within <see cref="LoggedHitWindowSeconds"/> (0 without one).</summary>
    public override double LoggedHitAmount(int damageLogId, EnemyTarget target, SimulationContext ctx)
    {
        if (!_hitsByKey.TryGetValue((damageLogId, new UnitRef(target.Id, target.InstanceId)), out var hits)) return 0.0;
        double now = ctx.CurrentTime.TotalSeconds;
        LogDamage? best = null;
        foreach (var h in hits)
        {
            if (h.Time < now - LoggedHitWindowSeconds || _taken.Contains(h)) continue;
            if (h.Time > now + LoggedHitWindowSeconds) break;
            if (best == null || Math.Abs(h.Time - now) < Math.Abs(best.Time - now)) best = h;
        }
        if (best == null) return 0.0;
        _taken.Add(best);
        return best.UnblockedAmount;
    }
}
