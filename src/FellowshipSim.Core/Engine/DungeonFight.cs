using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Core.Engine;

/// <summary>
/// One fight of a dungeon's pulls (<see cref="DungeonProfile.RunOrder"/>), to its last pull's end or its time limit. A pull
/// starts as the fight starts, as the pull its <c>chain_with</c> names ends, or its downtime after the pull before it ends.
/// Downtime has no enemies and no actions. A pull ends as its enemies, joined ones included, are all dead: an enemy with hit
/// points dies from damage, and an enemy without them as its pull duration ends. The hero's boss auras are on while a boss is
/// in combat.
/// </summary>
internal sealed class DungeonFight
{
    /// <summary>The health of an enemy whose health follows time; its size sets only damage-to-health ratios.</summary>
    private const double TimeHealth = 100_000_000;

    private readonly Hero _hero;
    private readonly SimulationContext _ctx;
    private readonly DungeonProfile _dungeon;
    private readonly double _varianceScale, _heroShare;
    private readonly Func<SimTime, Func<bool>, bool> _act;
    private readonly IReadOnlyList<RunPlan> _plans;
    private readonly SimTime _limit;
    private readonly Dictionary<EnemyTarget, Arrival> _arrivals = new(ReferenceEqualityComparer.Instance);
    private readonly Func<bool> _runEnded;

    private int _run;
    private RunState? _current;
    private bool _bossAuras = true;
    private SimTime _bossSince = SimTime.Infinite;
    private double _targetSeconds, _bossSeconds;
    // The health damage took from enemies with hit points, and the seconds of pulls so far, for the time-to-die estimates.
    private double _healthTaken;
    private SimTime _pullTime, _runStart, _downtimeEnd;

    /// <summary>
    /// A fight of <paramref name="dungeon"/>: <paramref name="act"/> has the hero act until a time or until a condition
    /// holds, and returns true when the condition stopped it. <paramref name="varianceScale"/> scales each pull duration.
    /// <paramref name="heroShare"/> (0 to 1) is the hero's share of the damage to enemies with hit points.
    /// </summary>
    public DungeonFight(Hero hero, SimulationContext ctx, DungeonProfile dungeon, double varianceScale, double heroShare, Func<SimTime, Func<bool>, bool> act)
    {
        _hero = hero;
        _ctx = ctx;
        _dungeon = dungeon;
        _varianceScale = varianceScale;
        _heroShare = heroShare;
        _act = act;
        _limit = SimTime.FromSeconds(dungeon.TimeLimitSeconds);
        var order = dungeon.RunOrder() ?? throw new ArgumentException(dungeon.Problem() ?? "The pulls refer to each other in a circle.", nameof(dungeon));
        _plans = order.Select(root => new RunPlan(root, dungeon.RunMembers(root), dungeon.HealthScale, varianceScale)).ToList();
        _runEnded = () => _current is { Alive: 0 };
        ctx.EnemyDemising += Demising;
        ctx.EnemyHurt += Hurt;
    }

    /// <summary>
    /// Runs the fight. Returns its end, the seconds each enemy was in combat, summed, and the seconds a boss was in combat.
    /// </summary>
    public (SimTime End, double TargetSeconds, double BossSeconds) Run()
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
            _runStart = _ctx.CurrentTime;
            Spawn(plan.Root);
            _ctx.Player.CurrentTarget = _current.Pulls[0].Enemies.FirstOrDefault(e => !e.IsPriority) ?? _current.Pulls[0].Enemies[0];
            Refresh();
            bool ended = _act(_limit, _runEnded);
            _pullTime += _ctx.CurrentTime - _runStart;
            _current = null;
            if (!ended) break;
        }
        var end = _ctx.CurrentTime < _limit ? _ctx.CurrentTime : _limit;
        foreach (var (enemy, arrival) in _arrivals)
        {
            if (!arrival.Gone) _targetSeconds += (end - arrival.At).TotalSeconds;
        }
        if (_bossSince != SimTime.Infinite) _bossSeconds += (end - _bossSince).TotalSeconds;
        _ctx.EnemyDemising -= Demising;
        _ctx.EnemyHurt -= Hurt;
        return (end, _targetSeconds, _bossSeconds);
    }

    // A pull's enemies enter combat in the current run, and its join is armed.
    private void Spawn(DungeonPull pull)
    {
        var run = _current!;
        var state = new PullState(pull);
        run.Pulls.Add(state);
        var now = _ctx.CurrentTime;
        int slot = 0;
        // A pull's priority targets come after its other enemies, so the hero starts on one of the others.
        foreach (var group in pull.Groups.OrderBy(g => g.Priority))
        {
            for (int i = 0; i < group.Count; i++)
            {
                double? hitPoints = group.HitPoints * _dungeon.HealthScale;
                var enemy = _ctx.AddEnemy(1000 + pull.Number, ++slot, group.Name, maxHealth: hitPoints ?? TimeHealth);
                enemy.IsBoss = group.Boss;
                enemy.IsPriority = group.Priority;
                if (hitPoints != null)
                {
                    enemy.SpiritPointValue = group.SpiritPoints;
                    enemy.FollowDamage(_ctx, _heroShare);
                }
                else
                {
                    // TODO: Spirit Points from an enemy whose health follows time.
                    enemy.SpiritPointValue = 0;
                    var dies = now + SimTime.FromSeconds(pull.PullDurationSeconds!.Value * _varianceScale);
                    enemy.FollowTime(_ctx, now, dies);
                    _ctx.ScheduleAt(dies, c => c.RemoveEnemy(enemy, defeated: true), "Pull duration");
                }
                if (group.Cast is { } cast) enemy.CastOnCooldown(_ctx, cast);
                state.Enemies.Add(enemy);
                _arrivals[enemy] = new Arrival(now, run, state, enemy.IsBoss && pull.Final);
                run.Alive++;
                run.Count++;
            }
        }

        if (pull.Join is not { } join) return;
        if (join.Triggers == 0) JoinNow(state);
        else if (join.OffsetMs is { } ms) _ctx.Schedule(SimTime.FromMilliseconds((long)Math.Round(ms)), _ => JoinNow(state), "Pull with");
        else if (join.OffsetPct is { } pct && pull.Groups.All(g => g.HitPoints == null))
        {
            // The pull's health follows time: its average falls to the percent at that share of its duration.
            var at = now + SimTime.FromSeconds(pull.PullDurationSeconds!.Value * _varianceScale * (1 - pct / 100.0));
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

    private void Hurt(SimulationContext ctx, EnemyTarget enemy, double healthLost)
    {
        if (!_arrivals.TryGetValue(enemy, out var arrival)) return;
        _healthTaken += healthLost;
        var host = arrival.Pull;
        if (host is { Joined: false, Pull.Join.OffsetPct: { } pct } && host.Enemies.Average(e => e.IsSleeping ? 0 : e.HealthPercent) <= pct)
        {
            JoinNow(host);
        }
        Estimate();
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
    /// Sets the next trash pull's and boss's starts and the fight's end from the current pull's time to die, the downtime and
    /// the pulls left. Health from damage falls at the rate it has fallen in the pulls so far; before any damage, a pull with
    /// hit points has no estimate.
    /// </summary>
    private void Estimate()
    {
        var now = _ctx.CurrentTime;
        double pullSeconds = (_pullTime + (_current != null ? now - _runStart : SimTime.Zero)).TotalSeconds;
        double rate = pullSeconds > 0 ? _healthTaken / pullSeconds : 0;
        // The time the run in progress ends, or the end of the downtime before the next run.
        double at;
        if (_current != null)
        {
            // The current pull: its enemies left, and the pulls not joined yet.
            double health = 0, timed = 0;
            foreach (var pull in _current.Pulls)
            {
                foreach (var enemy in pull.Enemies)
                {
                    if (_arrivals[enemy].Gone) continue;
                    if (enemy.HeroShare != null) health += enemy.CurrentHealth;
                    else timed = Math.Max(timed, (enemy.DiesAt - now).TotalSeconds);
                }
            }
            var plan = _plans[_run];
            foreach (var member in plan.Members)
            {
                if (_current.Pulls.Any(p => p.Pull == member)) continue;
                health += plan.HealthOf(member);
                timed = Math.Max(timed, plan.TimedOf(member));
            }
            at = now.TotalSeconds + Math.Max(timed, Seconds(health, rate));
        }
        else at = _downtimeEnd.TotalSeconds;

        SimTime nextTrash = SimTime.Infinite, nextBoss = SimTime.Infinite;
        int first = _current != null ? _run + 1 : _run;
        for (int i = first; i < _plans.Count; i++)
        {
            var plan = _plans[i];
            // In downtime, the next run starts at the downtime's end.
            if ((_current != null || i > first) && plan.Root.ChainWith == null) at += plan.Root.DowntimeSeconds;
            var start = double.IsInfinity(at) ? SimTime.Infinite : SimTime.FromSeconds(at);
            if (plan.HasTrash && nextTrash == SimTime.Infinite) nextTrash = start;
            if (plan.HasBoss && nextBoss == SimTime.Infinite) nextBoss = start;
            at += Math.Max(plan.Timed, Seconds(plan.Health, rate));
        }
        _ctx.NextTrashAt = nextTrash;
        _ctx.NextBossAt = nextBoss;
        _ctx.FightEnd = double.IsInfinity(at) || at >= _limit.TotalSeconds ? _limit : SimTime.FromSeconds(at);
    }

    private static double Seconds(double health, double rate) => health <= 0 ? 0 : rate > 0 ? health / rate : double.PositiveInfinity;

    /// <summary>A run's pulls with the health and time of each, from the profile.</summary>
    private sealed class RunPlan
    {
        public readonly DungeonPull Root;
        public readonly IReadOnlyList<DungeonPull> Members;
        public readonly double Health, Timed;
        public readonly bool HasTrash, HasBoss;
        private readonly double _healthScale, _varianceScale;

        public RunPlan(DungeonPull root, IReadOnlyList<DungeonPull> members, double healthScale, double varianceScale)
        {
            Root = root;
            Members = members;
            _healthScale = healthScale;
            _varianceScale = varianceScale;
            Health = members.Sum(HealthOf);
            Timed = members.Max(TimedOf);
            HasTrash = members.Any(m => m.Groups.Any(g => !g.Boss));
            HasBoss = members.Any(m => m.IsBoss);
        }

        public double HealthOf(DungeonPull pull) => pull.Groups.Sum(g => g.Count * (g.HitPoints ?? 0)) * _healthScale;

        public double TimedOf(DungeonPull pull) =>
            pull.Groups.Any(g => g.HitPoints == null) ? pull.PullDurationSeconds!.Value * _varianceScale : 0;
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
