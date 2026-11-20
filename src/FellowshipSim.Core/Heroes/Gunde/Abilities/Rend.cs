namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Rend (ability 2286, effect 3231, damage logged as 2286): "Damage done by your abilities build up this bleed, damaging
/// the target over time." Each enemy's Rend is the damage its applications added. Gunde never casts it: his abilities
/// apply it (<see cref="RendCast"/>). Combat logs (n7zxK8harZT2wjDB fight 9,
/// Y4wpb2f3gc8LqJDX fight 25):
/// <list type="bullet">
/// <item>each application deals its damage over <see cref="TicksPerApplication"/> ticks, an equal share each, on the
/// enemy's tick timer, which starts at the first application and keeps its rhythm through later ones (Chains#3: each
/// tick is an equal share of all applications so far);</item>
/// <item>an application ends after its own ticks (Bloodfang#4 @ 139.560: the tick drops by exactly the
/// applications that ticked first);</item>
/// <item>ticks come every <see cref="TickSeconds"/>, not hasted; Deep Rend's ticks come faster, on whole server frames,
/// and each application deals one more tick of an equal share;</item>
/// <item>ticks never crit and take no damage multipliers from Gunde: the damage an application adds is already dealt
/// damage. They take the target's incoming-damage multipliers (Blueprint preset <c>GundeRend</c>, build 26320967).</item>
/// </list>
/// The game keeps each enemy's Rend as buckets (Blueprint DotManager, build 26320967): Deep Rend makes 11 of them, each
/// tick dealing its bucket times Deep Rend's rate. <see cref="RawTotal"/> is the buckets' sum and
/// <see cref="Remaining"/> the damage they deal.
/// </summary>
public sealed partial class Rend : GundeAbility
{
    public const int DebuffEffectId = 3231;
    /// <summary>Seconds between ticks (game data: <c>period</c> of ability 2286).</summary>
    public const double TickSeconds = AbilityPeriod;
    /// <summary>Ticks of one application: its <c>duration</c> over its <c>period</c> (game data, ability 2286).</summary>
    public const int TicksPerApplication = AbilityDuration / AbilityPeriod;

    /// <summary>"Each stack of Rend corresponds to a share of Strength damage over its full duration."</summary>
    public static readonly AuraDef Debuff = AuraDef.Effect(AbilityName, DebuffEffectId, maxStacks: 9999) with { Duration = SimTime.Infinite };

    private readonly Dictionary<EnemyTarget, Bleed> _bleeds = new();
    private SimulationContext? _fight;

    public Rend(Gunde owner) : base(owner, AbilityId, AbilityName) { }

    /// <summary>How much faster Rend ticks (Deep Rend raises it). Each application ticks this many times more, in equal shares.</summary>
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
        if (bleed.NextTick == null) bleed.NextTick = ctx.Schedule(SimTime.FromSeconds(TickPeriodSeconds), c => Tick(c, target), AbilityName);
        UpdateDebuff(target, ctx.CurrentTime);
    }

    /// <summary>The damage <paramref name="target"/>'s Rend has still to deal, Deep Rend's included (Exsanguinate, Slaughter).</summary>
    public double Remaining(EnemyTarget target)
    {
        if (!_bleeds.TryGetValue(target, out var bleed)) return 0.0;
        double left = 0.0;
        foreach (var a in bleed.Applications) left += a.PerTick * a.TicksLeft;
        return left;
    }

    /// <summary>
    /// The damage <paramref name="target"/>'s Rend holds, without Deep Rend's: the sum of the game's buckets
    /// (<c>GetTotalDamage</c>), which deal <see cref="TickRate"/> times their damage as they tick (debuff stacks, Sundered
    /// Flesh).
    /// </summary>
    public double RawTotal(EnemyTarget target) => Remaining(target) / TickRate;

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
        if (applications.Count > 0) bleed.NextTick = ctx.Schedule(SimTime.FromSeconds(TickPeriodSeconds), c => Tick(c, target), AbilityName);
        else
        {
            _bleeds.Remove(target);
            target.Auras.Remove(Debuff, ctx.CurrentTime);
        }

        // A forced non-crit rolls no spread: the preset GundeRend has no crit and no spread (Blueprint, build 26320967).
        ctx.ApplyDamage(Gunde, target, damage * Gunde.DamageMultiplierAgainst(target, ctx.CurrentTime), AbilityId, AbilityName, canCrit: false, 0.0, 1.0,
            forcedCrit: false);
        UpdateDebuff(target, ctx.CurrentTime);
        Ticked?.Invoke(ctx, target);
        Gunde.CheckSunderedFlesh(ctx, target, RawTotal(target));
    }

    /// <summary>
    /// The debuff's stacks: the raw total in Strength, rounded, and at least one (Blueprint <c>UpdateDamageVisuals</c>,
    /// build 26320967). Unreal's <c>Round</c> takes halves away from zero.
    /// </summary>
    private void UpdateDebuff(EnemyTarget target, SimTime now)
    {
        if (!_bleeds.ContainsKey(target)) return;
        double strength = Math.Max(1.0, Gunde.PrimaryAttribute(now));
        int stacks = Math.Max(1, (int)Math.Round(RawTotal(target) / strength, MidpointRounding.AwayFromZero));
        target.Auras.Apply(Debuff, now, setStacks: Math.Min(stacks, Debuff.MaxStacks));
    }

    /// <summary>Rend has no cast: <see cref="Apply"/> adds to it.</summary>
    public override void Execute(SimulationContext ctx, EnemyTarget target) { }

    private sealed class Bleed
    {
        public List<Application> Applications { get; } = new();
        public ScheduledEvent? NextTick { get; set; }
    }

    private readonly record struct Application(double PerTick, int TicksLeft);
}
