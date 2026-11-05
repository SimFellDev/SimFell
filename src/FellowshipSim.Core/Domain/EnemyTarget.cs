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

    /// <summary>True for a boss.</summary>
    public bool IsBoss { get; set; }

    /// <summary>True for a priority target of a trash pull.</summary>
    public bool IsPriority { get; set; }

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

    /// <summary>The Spirit Points this enemy is worth (heroes earn them as their damage takes its health down).</summary>
    public double SpiritPointValue { get; set; }

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
