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

    /// <summary>The time the enemy's health reaches 0 (<see cref="FollowTime"/>); <see cref="SimTime.Infinite"/> when its health follows damage.</summary>
    public SimTime DiesAt { get; private set; } = SimTime.Infinite;

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
        DiesAt = end;
    }
    public double HealthPercent => HealthFraction * 100.0;
    public double HealthFraction => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0.0;

    /// <summary>The Spirit Points this enemy is worth (heroes earn them as their damage takes its health down).</summary>
    public double SpiritPointValue { get; set; }

    public EnemyTarget(int id, int instanceId = 1, string name = "Enemy")
    {
        Id = id;
        InstanceId = instanceId;
        Name = name;
    }
}

