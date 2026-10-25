namespace FellowshipSim.Core.Domain;

public class EnemyTarget : Actor
{
    public double MaxHealth { get; set; } = 1_000_000;

    /// <summary>Health left: taken down by damage, or by the fight's time once <see cref="FollowTime"/> is called.</summary>
    public double CurrentHealth
    {
        get => _clock == null ? field : MaxHealth * Math.Max(0.0, 1.0 - _clock.CurrentTime.TotalSeconds / _healthSeconds);
        set;
    } = 1_000_000;

    private SimulationContext? _clock;
    private double _healthSeconds;

    /// <summary>From now on the health falls with <paramref name="ctx"/>'s time, from full at 0 to none at <paramref name="end"/>.</summary>
    public void FollowTime(SimulationContext ctx, SimTime end)
    {
        _clock = ctx;
        _healthSeconds = Math.Max(0.001, end.TotalSeconds);
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

