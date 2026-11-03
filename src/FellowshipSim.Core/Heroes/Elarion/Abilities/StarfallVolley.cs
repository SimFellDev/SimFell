namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 126: "Fire a volley of celestial arrows into the sky that pelt the area around target enemy for 8 seconds,
/// dealing magic damage every 1 seconds to the target and any nearby enemies. Starfall Volley follows the target for its
/// duration." 30 Focus, cooldown 40s; damage falls off as sqrt(12/N) beyond 12 enemies (targetCountFalloff 12).
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23, 44 volleys): the arrows land on each enemy at its
/// own moments, 0.5-0.8s apart at the median for one enemy, the first 0.2-0.4s after the cast, and each hit deals the
/// same damage on every enemy. UNVERIFIED: an arrow on every enemy as the volley is cast and every hasted second after
/// (an enemy takes up to 2-3 arrows in some hasted seconds).
/// </summary>
public class StarfallVolley : ElarionAbility
{
    public const int AbilityId = 126;
    public const string AbilityName = "Starfall Volley";
    /// <summary>Placeholder StarfallDmg: 100% of Agility per hit.</summary>
    public const double Coefficient = 1.0;
    public const double Cost = 30.0;
    public const double CooldownSeconds = 40.0;
    public const double DurationSeconds = 8.0;
    public const double PeriodSeconds = 1.0;
    public const double TargetThreshold = 12;
    /// <summary>Combat logs: an arrow lands 0.2-0.4s after it is released; the engine releases none in the last 0.3s.</summary>
    public const double FallSeconds = 0.3;

    /// <summary>
    /// The choice of each enemy a release strikes: certain in a sim; a replay takes the enemies the log shows an arrow
    /// landing on before the next release. The arrows of one release land over most of a period, so they form no
    /// multi-target hit a replay could take whole.
    /// </summary>
    public const string ArrowProcKey = "starfall_volley_arrow";

    /// <summary>Seconds added to every volley (Astronomer's Hail).</summary>
    public double ExtraDurationSeconds { get; set; }

    /// <summary>Multiplier on the hits on the volley's own target (Astronomer's Hail).</summary>
    public double MainTargetMultiplier { get; set; } = 1.0;

    /// <summary>A volley began or ended falling: the number of volleys falling now (Skylit Grace).</summary>
    public event Action<SimulationContext, int>? CountChanged;

    private readonly List<Volley> _volleys = new();

    public StarfallVolley(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power)
    {
        BaseCoefficient = Coefficient;
        FocusCost = Cost;
        SetCooldown(CooldownSeconds);
        AoeTargetThreshold = TargetThreshold;
    }

    /// <summary>The volleys falling now (they can overlap).</summary>
    public int ActiveCount => _volleys.Count;

    /// <summary>Adds <paramref name="seconds"/> to every volley falling now (Astronomer's Hail).</summary>
    public void Extend(SimulationContext ctx, double seconds)
    {
        foreach (var volley in _volleys)
        {
            volley.End += SimTime.FromSeconds(seconds);
            volley.Ending?.Cancel();
            ScheduleEnd(ctx, volley);
        }
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Pay(ctx, CostAt(ctx.CurrentTime));
        var volley = new Volley(target, ctx.CurrentTime + SimTime.FromSeconds(DurationSeconds + ExtraDurationSeconds));
        _volleys.Add(volley);
        CountChanged?.Invoke(ctx, _volleys.Count);
        ScheduleEnd(ctx, volley);
        Pelt(ctx, volley);
    }

    private void ScheduleEnd(SimulationContext ctx, Volley volley) =>
        volley.Ending = ctx.ScheduleAt(volley.End, c =>
        {
            volley.Timer?.Cancel();
            _volleys.Remove(volley);
            CountChanged?.Invoke(c, _volleys.Count);
        }, Name);

    private void Pelt(SimulationContext ctx, Volley volley)
    {
        // Arrows that could not land before the volley ends are not released.
        if (ctx.CurrentTime > volley.End - SimTime.FromSeconds(FallSeconds)) return;
        var enemies = ctx.ActiveEnemies;
        var struck = new List<EnemyTarget>(enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
        {
            if (ctx.Outcomes.RollProc(ArrowProcKey, 1.0, ctx, enemies[i])) struck.Add(enemies[i]);
        }
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            var enemy = struck[i];
            DealDamage(ctx, enemy, damageMultiplier: falloff * (enemy == volley.Target ? MainTargetMultiplier : 1.0));
        }
        volley.Timer = HastedTimer.Start(ctx, Owner, PeriodSeconds, c => Pelt(c, volley), Name);
    }

    public override void ResetState() => _volleys.Clear();

    private sealed class Volley(EnemyTarget target, SimTime end)
    {
        public EnemyTarget Target { get; } = target;
        public SimTime End { get; set; } = end;
        public HastedTimer? Timer { get; set; }
        public ScheduledEvent? Ending { get; set; }
    }
}
