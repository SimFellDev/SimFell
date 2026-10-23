namespace FellowshipSim.Core.Heroes.Gunde;

/// <summary>
/// Rend (effect 3231, damage logged as 2286): "Damage done by your abilities build up this bleed, damaging the target
/// over time." Each enemy's Rend is the damage its applications added. Combat logs (n7zxK8harZT2wjDB fight 9,
/// Y4wpb2f3gc8LqJDX fight 25):
/// <list type="bullet">
/// <item>each application deals its damage over 10 ticks, a tenth each, on the enemy's tick timer, which starts at the
/// first application and keeps its rhythm through later ones (Chains#3: ticks 317, 363, 419 for applications of
/// 3169, then +461, then +563, each a tenth of all applications so far);</item>
/// <item>an application ends after its own ticks (Bloodfang#4 @ 139.560: the tick drops by exactly the three
/// applications that ticked first);</item>
/// <item>ticks come every 3s, not hasted; Deep Rend's ticks come 10% faster (82 server frames, 2.733s) and each
/// application deals 11 ticks of a tenth;</item>
/// <item>ticks never crit and take no damage multipliers: the damage an application adds is already dealt damage.</item>
/// </list>
/// </summary>
public sealed class Rend
{
    public const int DamageId = 2286;
    public const string DamageName = "Rend";
    public const int DebuffEffectId = 3231;
    public const double TickSeconds = 3.0;
    public const int TicksPerApplication = 10;

    /// <summary>"Each stack of Rend corresponds to 100% Strength damage over its full duration."</summary>
    public static readonly AuraDef Debuff = AuraDef.Effect(DamageName, DebuffEffectId, maxStacks: 9999) with { Duration = SimTime.Infinite };

    private readonly Gunde _gunde;
    private readonly Dictionary<EnemyTarget, Bleed> _bleeds = new();
    private SimulationContext? _fight;

    public Rend(Gunde gunde) => _gunde = gunde;

    /// <summary>How much faster Rend ticks (Deep Rend: 1.1). Each application ticks this many times more, at a tenth each.</summary>
    public double TickRate { get; set; } = 1.0;

    private int Ticks => (int)Math.Round(TicksPerApplication * TickRate);

    /// <summary>Seconds between ticks: the game ticks on whole server frames, so a shortened period rounds up to one.</summary>
    private double TickPeriodSeconds => Math.Ceiling(TickSeconds / TickRate / PeriodicEffect.ServerFrameSeconds - 1e-6) * PeriodicEffect.ServerFrameSeconds;

    /// <summary>A Rend tick dealt its damage to an enemy (Blood Feathers).</summary>
    public event Action<SimulationContext, EnemyTarget>? Ticked;

    /// <summary>An enemy with Rend on it died (Blood Feathers).</summary>
    public event Action<SimulationContext, EnemyTarget>? EnemyDiedWithRend;

    /// <summary>Adds <paramref name="damage"/> to <paramref name="target"/>'s Rend.</summary>
    public void Apply(SimulationContext ctx, EnemyTarget target, double damage)
    {
        if (damage <= 0.0 || target == null || target.IsSleeping) return;
        NewFight(ctx);
        if (!_bleeds.TryGetValue(target, out var bleed))
        {
            bleed = new Bleed();
            _bleeds[target] = bleed;
        }
        bleed.Applications.Add(new Application(damage / TicksPerApplication, Ticks));
        if (bleed.NextTick == null) bleed.NextTick = ctx.Schedule(SimTime.FromSeconds(TickPeriodSeconds), c => Tick(c, target), DamageName);
        UpdateDebuff(target, ctx.CurrentTime);
        _gunde.CheckSunderedFlesh(ctx, target);
    }

    /// <summary>All the damage of <paramref name="target"/>'s Rend applications, dealt or not ("the Total Rend Damage active on the target").</summary>
    public double Total(EnemyTarget target)
    {
        if (!_bleeds.TryGetValue(target, out var bleed)) return 0.0;
        double total = 0.0;
        foreach (var a in bleed.Applications) total += a.PerTick * Ticks;
        return total;
    }

    /// <summary>The damage <paramref name="target"/>'s Rend has still to deal.</summary>
    public double Remaining(EnemyTarget target)
    {
        if (!_bleeds.TryGetValue(target, out var bleed)) return 0.0;
        double left = 0.0;
        foreach (var a in bleed.Applications) left += a.PerTick * a.TicksLeft;
        return left;
    }

    public bool IsOn(EnemyTarget target) => _bleeds.ContainsKey(target);

    /// <summary>Removes <paramref name="target"/>'s Rend; returns the damage it had still to deal (Slaughter).</summary>
    public double Consume(SimulationContext ctx, EnemyTarget target)
    {
        double left = Remaining(target);
        if (_bleeds.Remove(target, out var bleed)) bleed.NextTick?.Cancel();
        target.Auras.Remove(Debuff, ctx.CurrentTime);
        return left;
    }

    public void Reset()
    {
        _bleeds.Clear();
        _fight = null;
    }

    private void NewFight(SimulationContext ctx)
    {
        if (_fight == ctx) return;
        // A new fight: forget the last one's bleeds, and end the ones on enemies that die in this one.
        _bleeds.Clear();
        _fight = ctx;
        ctx.EnemyDemising += (c, enemy) =>
        {
            if (!_bleeds.Remove(enemy, out var bleed)) return;
            bleed.NextTick?.Cancel();
            EnemyDiedWithRend?.Invoke(c, enemy);
        };
    }

    private void Tick(SimulationContext ctx, EnemyTarget target)
    {
        if (!_bleeds.TryGetValue(target, out var bleed)) return;
        bleed.NextTick = null;
        double damage = 0.0;
        var applications = bleed.Applications;
        for (int i = 0; i < applications.Count; i++)
        {
            var a = applications[i];
            damage += a.PerTick;
            applications[i] = a with { TicksLeft = a.TicksLeft - 1 };
        }
        applications.RemoveAll(a => a.TicksLeft <= 0);
        if (applications.Count > 0) bleed.NextTick = ctx.Schedule(SimTime.FromSeconds(TickPeriodSeconds), c => Tick(c, target), DamageName);
        else
        {
            _bleeds.Remove(target);
            target.Auras.Remove(Debuff, ctx.CurrentTime);
        }

        ctx.ApplyDamage(_gunde, target, damage, DamageId, DamageName, canCrit: false, 0.0, 1.0);
        UpdateDebuff(target, ctx.CurrentTime);
        Ticked?.Invoke(ctx, target);
        if (_bleeds.ContainsKey(target)) _gunde.CheckSunderedFlesh(ctx, target);
    }

    /// <summary>The debuff's stacks: the damage still to deal, in Strength (rounded up).</summary>
    private void UpdateDebuff(EnemyTarget target, SimTime now)
    {
        if (!_bleeds.ContainsKey(target)) return;
        double strength = Math.Max(1.0, _gunde.PrimaryAttribute(now));
        int stacks = Math.Max(1, (int)Math.Ceiling(Remaining(target) / strength - 1e-9));
        target.Auras.Apply(Debuff, now, setStacks: Math.Min(stacks, Debuff.MaxStacks));
    }

    private sealed class Bleed
    {
        public List<Application> Applications { get; } = new();
        public ScheduledEvent? NextTick { get; set; }
    }

    private readonly record struct Application(double PerTick, int TicksLeft);
}
