using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Core.Domain;

public class EnemyTarget : Actor
{
    public double MaxHealth { get; set; } = 1_000_000;

    /// <summary>Health left: taken down by damage, or by the fight's time once <see cref="FollowTime"/> is called.</summary>
    public double CurrentHealth
    {
        get => _clock == null ? field
            : MaxHealth * Math.Clamp(1.0 - (_clock.CurrentTime.TotalSeconds - _healthStart) / _healthSeconds, 0.0, 1.0);
        set;
    } = 1_000_000;

    private SimulationContext? _clock;
    private double _healthStart, _healthSeconds;
    private SimTime _diesAt = SimTime.Infinite;

    /// <summary>
    /// The time the enemy's health reaches 0: the end of <see cref="FollowTime"/>. <see cref="SimTime.Infinite"/> for an
    /// enemy whose health follows damage.
    /// </summary>
    public SimTime DiesAt => _diesAt;

    /// <summary>The hero's damage to the enemy so far.</summary>
    public double HeroDamage { get; internal set; }

    /// <summary>True once the enemy has died, until it arises.</summary>
    public bool HasDied { get; internal set; }

    /// <summary>True for a boss.</summary>
    public bool IsBoss { get; set; }

    /// <summary>True for a priority target of a trash pull.</summary>
    public bool IsPriority { get; set; }

    /// <summary>True for a training dummy (the game's <c>ActorTypeTags.TrainingDummy</c>).</summary>
    public bool IsTrainingDummy { get; set; }

    /// <summary>
    /// From now on the health falls with <paramref name="ctx"/>'s time, from full at <paramref name="start"/> to none at
    /// <paramref name="end"/>.
    /// </summary>
    public void FollowTime(SimulationContext ctx, SimTime start, SimTime end)
    {
        _clock = ctx;
        _healthStart = start.TotalSeconds;
        _healthSeconds = Math.Max(0.001, (end - start).TotalSeconds);
        _diesAt = end;
    }

    /// <summary>True once <see cref="FollowTime"/> is called.</summary>
    public bool HealthFollowsTime => _clock != null;

    public double HealthPercent => HealthFraction * 100.0;
    public double HealthFraction => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0.0;

    /// <summary>The Spirit Point value of this enemy (<see cref="Hero.EarnSpiritPoints"/>).</summary>
    public double SpiritPointValue { get; set; }

    /// <summary>
    /// The health fraction <see cref="Hero.EarnSpiritPoints"/> has counted down to. One loss reaches a replay twice, from the hit and from
    /// the log's health, so a fraction above this one gives no Spirit Points until a heal (<see cref="HealedTo"/>).
    /// </summary>
    public double SpiritHealthFraction { get; set; } = 1.0;

    /// <summary>A heal took the enemy to <paramref name="healthFraction"/>: the health lost again earns Spirit Points again.</summary>
    public void HealedTo(double healthFraction) => SpiritHealthFraction = Math.Max(SpiritHealthFraction, healthFraction);

    /// <summary>The ability the enemy is casting; null when it is not casting.</summary>
    public string? CastAbility { get; private set; }

    /// <summary>The start of the enemy's cast.</summary>
    public SimTime CastStart { get; private set; }

    /// <summary>The end of the enemy's cast.</summary>
    public SimTime CastEnd { get; private set; }

    /// <summary>True while the enemy is casting.</summary>
    public bool IsCasting => CastAbility != null;

    private ScheduledEvent? _nextCast, _castEnd;

    /// <summary>
    /// The enemy casts <paramref name="cast"/> on its cooldown, the first cast one cooldown from now. A cast completes with
    /// no effect.
    /// </summary>
    public void CastOnCooldown(SimulationContext ctx, EnemyCast cast)
    {
        var cooldown = SimTime.FromSeconds(cast.CooldownSeconds);
        void Begin(SimulationContext c)
        {
            CastAbility = cast.Ability;
            CastStart = c.CurrentTime;
            CastEnd = c.CurrentTime + SimTime.FromSeconds(cast.CastSeconds);
            _castEnd = c.ScheduleAt(CastEnd, _ => StopCast(), cast.Ability);
            _nextCast = c.Schedule(cooldown, Begin, cast.Ability);
        }
        _nextCast = ctx.Schedule(cooldown, Begin, cast.Ability);
    }

    /// <summary>
    /// The enemy begins casting <paramref name="ability"/> now, <paramref name="seconds"/> long. At its end
    /// <paramref name="completed"/> runs, unless the cast stopped first.
    /// </summary>
    public void BeginCast(SimulationContext ctx, string ability, double seconds, Action<SimulationContext>? completed = null)
    {
        StopCast();
        CastAbility = ability;
        CastStart = ctx.CurrentTime;
        CastEnd = ctx.CurrentTime + SimTime.FromSeconds(seconds);
        CastChanged?.Invoke(ctx, this);
        _castEnd = ctx.ScheduleAt(CastEnd, c =>
        {
            _castEnd = null;
            CastAbility = null;
            CastChanged?.Invoke(c, this);
            completed?.Invoke(c);
        }, ability);
    }

    /// <summary>Raised as the enemy begins a cast of <see cref="BeginCast"/> and as the cast ends, completed or stopped.</summary>
    public event Action<SimulationContext, EnemyTarget>? CastChanged;

    /// <summary>Raised as the hero interrupts the enemy's cast (<see cref="SimulationContext.Interrupt"/>).</summary>
    public event Action<SimulationContext, EnemyTarget>? Interrupted;

    /// <summary>Another party member stops the enemy's cast now. Returns false when it was not casting.</summary>
    public bool StopCastByParty(SimulationContext ctx)
    {
        if (!StopCast()) return false;
        CastChanged?.Invoke(ctx, this);
        return true;
    }

    internal void RaiseInterrupted(SimulationContext ctx)
    {
        CastChanged?.Invoke(ctx, this);
        Interrupted?.Invoke(ctx, this);
    }

    /// <summary>Ends the enemy's cast now with no effect. Returns false when it was not casting.</summary>
    internal bool StopCast()
    {
        if (CastAbility == null) return false;
        _castEnd?.Cancel();
        _castEnd = null;
        CastAbility = null;
        return true;
    }

    public override void Arise(SimulationContext ctx)
    {
        HeroDamage = 0;
        SpiritHealthFraction = 1.0;
        HasDied = false;
        base.Arise(ctx);
    }

    public override void Demise(SimulationContext ctx)
    {
        StopCast();
        _nextCast?.Cancel();
        _nextCast = null;
        base.Demise(ctx);
    }

    public EnemyTarget(int id, int instanceId = 1, string name = "Enemy")
    {
        Id = id;
        InstanceId = instanceId;
        Name = name;
    }
}
