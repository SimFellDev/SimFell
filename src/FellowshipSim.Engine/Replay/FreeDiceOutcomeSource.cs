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
}
