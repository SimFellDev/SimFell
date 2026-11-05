using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Core.Engine;

/// <summary>
/// One fight of a dungeon's pulls (<see cref="DungeonProfile.RunOrder"/>), to its last pull's end or its time limit. A pull
/// starts as the fight starts, as the pull its <c>chain_with</c> names ends, or its downtime after the pull before it ends.
/// Downtime has no enemies and no actions. Each enemy's health falls from full to none over its pull's
/// <see cref="DungeonPull.DurationSeconds"/> (times the fight's variance scale), whatever the hero does, and the enemy dies
/// as it reaches none. A <c>pull_with</c> offset in milliseconds scales with the duration. A pull ends as its enemies, joined
/// ones included, are all dead. The hero's boss auras are on while a
/// boss is in combat.
/// </summary>
internal sealed class DungeonFight
{
    private readonly Hero _hero;
    private readonly SimulationContext _ctx;
    private readonly DungeonProfile _dungeon;
    private readonly double _varianceScale;
    private readonly Func<SimTime, Func<bool>, bool> _act;
    private readonly IReadOnlyList<RunPlan> _plans;
    // Each pull's enemy id: 1001 for the first pull of the dungeon text, 1002 for the second, and so on.
    private readonly Dictionary<string, int> _enemyIds;
    private readonly SimTime _limit;
    private readonly Dictionary<EnemyTarget, Arrival> _arrivals = new(ReferenceEqualityComparer.Instance);
    private readonly Func<bool> _runEnded;

    private int _run;
    private RunState? _current;
    private bool _bossAuras = true;
    private SimTime _bossSince = SimTime.Infinite;
    private double _targetSeconds, _bossSeconds;
    private SimTime _downtimeEnd;

    /// <summary>
    /// A fight of <paramref name="dungeon"/>: <paramref name="act"/> has the hero act until a time or until a condition
    /// holds, and returns true when the condition stopped it. <paramref name="varianceScale"/> scales each pull duration.
    /// </summary>
    public DungeonFight(Hero hero, SimulationContext ctx, DungeonProfile dungeon, double varianceScale, Func<SimTime, Func<bool>, bool> act)
    {
        _hero = hero;
        _ctx = ctx;
        _dungeon = dungeon;
        _varianceScale = varianceScale;
        _act = act;
        _limit = SimTime.FromSeconds(dungeon.TimeLimitSeconds);
        var order = dungeon.RunOrder() ?? throw new ArgumentException(dungeon.Problem() ?? "The pulls refer to each other in a circle.", nameof(dungeon));
        _plans = order.Select(root => new RunPlan(root, dungeon.RunMembers(root), varianceScale)).ToList();
        _enemyIds = dungeon.Pulls.Select((p, i) => (p.Name, Id: 1001 + i)).ToDictionary(x => x.Name, x => x.Id);
        _runEnded = () => _current is { Alive: 0 };
        ctx.EnemyDemising += Demising;
    }

    /// <summary>
    /// Runs the fight. Returns its end, the seconds each enemy was in combat, summed, the seconds a boss was in combat, and
    /// each enemy that entered combat.
    /// </summary>
    public (SimTime End, double TargetSeconds, double BossSeconds, IReadOnlyList<PartyEnemy> Enemies) Run()
    {
        for (_run = 0; _run < _plans.Count; _run++)
        {
            var plan = _plans[_run];
            if (_run > 0 && plan.Root.ChainWith == null && plan.Root.DowntimeSeconds > 0)
            {
                // Downtime: no enemies and no actions; cooldowns, regeneration and auras run on.
                _downtimeEnd = _ctx.CurrentTime + SimTime.FromSeconds(plan.Root.DowntimeSeconds);
                Refresh();
                _ctx.RunUntil(_downtimeEnd < _limit ? _downtimeEnd : _limit);
                if (_ctx.CurrentTime >= _limit) break;
            }
            _current = new RunState();
            Spawn(plan.Root);
            _ctx.Player.CurrentTarget = _current.Pulls[0].Enemies.FirstOrDefault(e => !e.IsPriority) ?? _current.Pulls[0].Enemies[0];
            Refresh();
            bool ended = _act(_limit, _runEnded);
            _current = null;
            if (!ended) break;
        }
        var end = _ctx.CurrentTime < _limit ? _ctx.CurrentTime : _limit;
        var enemies = new List<PartyEnemy>(_arrivals.Count);
        foreach (var (enemy, arrival) in _arrivals)
        {
            if (!arrival.Gone) _targetSeconds += (end - arrival.At).TotalSeconds;
            enemies.Add(new PartyEnemy(arrival.At, enemy.DiesAt, enemy.MaxHealth, enemy.HeroDamage));
        }
        if (_bossSince != SimTime.Infinite) _bossSeconds += (end - _bossSince).TotalSeconds;
        _ctx.EnemyDemising -= Demising;
        return (end, _targetSeconds, _bossSeconds, enemies);
    }

    // A pull's enemies enter combat in the current run, and its join is armed.
    private void Spawn(DungeonPull pull)
    {
        var run = _current!;
        var state = new PullState(pull);
        run.Pulls.Add(state);
        var now = _ctx.CurrentTime;
        var dies = now + SimTime.FromSeconds(pull.DurationSeconds * _varianceScale);
        int slot = 0;
        // A pull's priority targets come after its other enemies, so the hero starts on one of the others.
        foreach (var group in pull.Groups.OrderBy(g => g.Priority))
        {
            for (int i = 0; i < group.Count; i++)
            {
                var enemy = _ctx.AddEnemy(_enemyIds[pull.Name], ++slot, group.Name, maxHealth: group.Health!.Value * _dungeon.HealthScale);
                enemy.IsBoss = group.Boss;
                enemy.IsPriority = group.Priority;
                enemy.SpiritPointValue = group.Spirit;
                enemy.FollowTime(_ctx, now, dies);
                _ctx.ScheduleAt(dies, c => c.RemoveEnemy(enemy, defeated: true), "Pull duration");
                if (group.Cast is { } cast) enemy.CastOnCooldown(_ctx, cast);
                state.Enemies.Add(enemy);
                _arrivals[enemy] = new Arrival(now, run, state, enemy.IsBoss && pull.Final);
                run.Alive++;
                run.Count++;
            }
        }

        if (pull.Join is not { } join) return;
        if (join.Triggers == 0) JoinNow(state);
        // The offset scales with the pull's duration.
        else if (join.OffsetMs is { } ms) _ctx.Schedule(SimTime.FromMilliseconds((long)Math.Round(ms * _varianceScale)), _ => JoinNow(state), "Pull with");
        else if (join.OffsetPct is { } pct)
        {
            // The pull's health falls to the percent at that share of its duration.
            var at = now + SimTime.FromSeconds(pull.DurationSeconds * _varianceScale * (1 - pct / 100.0));
            _ctx.ScheduleAt(at, _ => JoinNow(state), "Pull with");
        }
    }

    // The pull the host's pull_with names joins the host's run, once.
    private void JoinNow(PullState host)
    {
        if (host.Joined || _current == null || !_current.Pulls.Contains(host)) return;
        host.Joined = true;
        Spawn(_dungeon.Pull(host.Pull.Join!.Pull)!);
        Refresh();
    }

    private void Demising(SimulationContext ctx, EnemyTarget enemy)
    {
        if (!_arrivals.TryGetValue(enemy, out var arrival) || arrival.Gone) return;
        arrival.Gone = true;
        _targetSeconds += (ctx.CurrentTime - arrival.At).TotalSeconds;
        var run = arrival.Run;
        var pull = arrival.Pull;
        pull.Deaths++;
        run.Alive--;
        run.Deaths++;
        if (run != _current) return;
        foreach (var host in run.Pulls.ToList())
        {
            if (host.Joined || host.Pull.Join is not { } join) continue;
            bool due = run.Alive == 0
                || join.AfterDeaths is { } deaths && host.Deaths >= deaths.Of(host.Enemies.Count)
                || join.AfterDeathsTotal is { } total && run.Deaths >= total.Of(run.Count);
            if (due) JoinNow(host);
        }
        Refresh();
    }

    // The fight's boss, trash and final boss state, the boss auras, and the estimates the action list reads.
    private void Refresh()
    {
        bool trash = false, boss = false, final = false;
        if (_current != null)
        {
            foreach (var pull in _current.Pulls)
            {
                foreach (var enemy in pull.Enemies)
                {
                    if (_arrivals[enemy].Gone) continue;
                    if (enemy.IsBoss) boss = true;
                    else trash = true;
                    if (_arrivals[enemy].FinalBoss) final = true;
                }
            }
        }
        _ctx.Trash = trash;
        _ctx.Boss = boss;
        _ctx.FinalBoss = final;
        var now = _ctx.CurrentTime;
        if (boss && _bossSince == SimTime.Infinite) _bossSince = now;
        if (!boss && _bossSince != SimTime.Infinite)
        {
            _bossSeconds += (now - _bossSince).TotalSeconds;
            _bossSince = SimTime.Infinite;
        }
        if (_bossAuras != boss)
        {
            _bossAuras = boss;
            foreach (var aura in _hero.BossAuras)
            {
                if (boss) _hero.Auras.Apply(aura, now);
                else _hero.Auras.Remove(aura, now);
            }
        }
        Estimate();
    }

    /// <summary>
    /// Sets the next trash pull's and boss's starts and the fight's end from the current run's enemies left, the pulls it has
    /// not joined yet, the downtime and the pulls left, each at its duration.
    /// </summary>
    private void Estimate()
    {
        var now = _ctx.CurrentTime;
        // The time the run in progress ends, or the end of the downtime before the next run.
        double at;
        if (_current != null)
        {
            double left = 0;
            foreach (var pull in _current.Pulls)
            {
                foreach (var enemy in pull.Enemies)
                {
                    if (!_arrivals[enemy].Gone) left = Math.Max(left, (enemy.DiesAt - now).TotalSeconds);
                }
            }
            var plan = _plans[_run];
            foreach (var member in plan.Members)
            {
                if (_current.Pulls.Any(p => p.Pull == member)) continue;
                left = Math.Max(left, plan.SecondsOf(member));
            }
            at = now.TotalSeconds + left;
        }
        else at = _downtimeEnd.TotalSeconds;

        SimTime nextTrash = SimTime.Infinite, nextBoss = SimTime.Infinite;
        int first = _current != null ? _run + 1 : _run;
        for (int i = first; i < _plans.Count; i++)
        {
            var plan = _plans[i];
            // In downtime, the next run starts at the downtime's end.
            if ((_current != null || i > first) && plan.Root.ChainWith == null) at += plan.Root.DowntimeSeconds;
            var start = SimTime.FromSeconds(at);
            if (plan.HasTrash && nextTrash == SimTime.Infinite) nextTrash = start;
            if (plan.HasBoss && nextBoss == SimTime.Infinite) nextBoss = start;
            at += plan.Seconds;
        }
        _ctx.NextTrashAt = nextTrash;
        _ctx.NextBossAt = nextBoss;
        _ctx.FightEnd = at >= _limit.TotalSeconds ? _limit : SimTime.FromSeconds(at);
    }

    /// <summary>A run's pulls with the seconds of each, from the profile.</summary>
    private sealed class RunPlan
    {
        public readonly DungeonPull Root;
        public readonly IReadOnlyList<DungeonPull> Members;
        /// <summary>The run's seconds: its longest pull's.</summary>
        public readonly double Seconds;
        public readonly bool HasTrash, HasBoss;
        private readonly double _varianceScale;

        public RunPlan(DungeonPull root, IReadOnlyList<DungeonPull> members, double varianceScale)
        {
            Root = root;
            Members = members;
            _varianceScale = varianceScale;
            Seconds = members.Max(SecondsOf);
            HasTrash = members.Any(m => m.Groups.Any(g => !g.Boss));
            HasBoss = members.Any(m => m.IsBoss);
        }

        public double SecondsOf(DungeonPull pull) => pull.DurationSeconds * _varianceScale;
    }

    /// <summary>The run in progress: its pulls in combat, its enemies alive, dead and in all.</summary>
    private sealed class RunState
    {
        public readonly List<PullState> Pulls = [];
        public int Alive, Deaths, Count;
    }

    /// <summary>A pull in combat: its enemies, their deaths, and whether its join has triggered.</summary>
    private sealed class PullState(DungeonPull pull)
    {
        public readonly DungeonPull Pull = pull;
        public readonly List<EnemyTarget> Enemies = [];
        public int Deaths;
        public bool Joined;
    }

    /// <summary>An enemy's arrival: its time, run and pull, whether it is the final boss, and whether it is gone.</summary>
    private sealed class Arrival(SimTime at, RunState run, PullState pull, bool finalBoss)
    {
        public readonly SimTime At = at;
        public readonly RunState Run = run;
        public readonly PullState Pull = pull;
        public readonly bool FinalBoss = finalBoss;
        public bool Gone;
    }
}
