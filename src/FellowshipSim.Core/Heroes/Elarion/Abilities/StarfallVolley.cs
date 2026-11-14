namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 126: "Fire a volley of celestial arrows into the sky that pelt the area around target enemy, dealing magic
/// damage at a fixed period to the target and any nearby enemies. Starfall Volley follows the target for its
/// duration." Costs Focus and has a cooldown; damage falls off beyond a target count (targetCountFalloff, <see cref="Constants.TargetCountThreshold"/>).
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23, 44 volleys): the arrows land on each enemy at its
/// own moments, the first shortly after the cast, and each hit deals the
/// same damage on every enemy. UNVERIFIED: an arrow on every enemy as the volley is cast and every hasted period after
/// (an enemy takes more than one arrow in some hasted periods). Per the user: the period takes the haste of the cast, and
/// arrows fall until the end of the duration, one due at the end included.
/// </summary>
public partial class StarfallVolley : ElarionAbility
{
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

    /// <summary>A volley began falling (Skylit Grace).</summary>
    public event Action<SimulationContext>? VolleyBegan;

    /// <summary>A volley ended falling (Skylit Grace).</summary>
    public event Action<SimulationContext>? VolleyEnded;

    private readonly List<Volley> _volleys = new();

    public StarfallVolley(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.AgilityCoefficient;
        FocusCost = Constants.Cost;
        SetCooldown(AbilityCooldown);
        AoeTargetThreshold = Constants.TargetCountThreshold;
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
        var volley = new Volley(target, ctx.CurrentTime + SimTime.FromSeconds(AbilityDuration + ExtraDurationSeconds),
            AbilityPeriod / (1.0 + Owner.GetHaste(ctx.CurrentTime)));
        _volleys.Add(volley);
        VolleyBegan?.Invoke(ctx);
        ScheduleEnd(ctx, volley);
        Pelt(ctx, volley);
    }

    private void ScheduleEnd(SimulationContext ctx, Volley volley) => volley.Ending = ctx.ScheduleAt(volley.End, c => EndVolley(c, volley), Name);

    private void EndVolley(SimulationContext ctx, Volley volley)
    {
        // An arrow due at the same moment as the volley ends still falls: the end is queued again after it.
        if (volley.Timer is { IsPending: true } timer && timer.Time <= ctx.CurrentTime)
        {
            volley.Ending = ctx.ScheduleAt(ctx.CurrentTime, c => EndVolley(c, volley), Name);
            return;
        }
        volley.Timer?.Cancel();
        _volleys.Remove(volley);
        VolleyEnded?.Invoke(ctx);
    }

    private void Pelt(SimulationContext ctx, Volley volley)
    {
        var enemies = ctx.ActiveEnemies;
        var struck = new List<EnemyTarget>(enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
        {
            if (ctx.Outcomes.RollProc(ArrowProcKey, 1.0, ctx, enemies[i])) struck.Add(enemies[i]);
        }
        double falloff = struck.Count > Constants.TargetCountThreshold ? Math.Sqrt((double)Constants.TargetCountThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            var enemy = struck[i];
            DealDamage(ctx, enemy, damageMultiplier: falloff * (enemy == volley.Target ? MainTargetMultiplier : 1.0));
        }
        volley.Timer = ctx.Schedule(SimTime.FromSeconds(volley.PeriodSeconds), c => Pelt(c, volley), Name);
    }

    public override void ResetState() => _volleys.Clear();

    private sealed class Volley(EnemyTarget target, SimTime end, double periodSeconds)
    {
        public EnemyTarget Target { get; } = target;
        public SimTime End { get; set; } = end;
        /// <summary>Seconds between arrows, with the haste of the cast.</summary>
        public double PeriodSeconds { get; } = periodSeconds;
        public ScheduledEvent? Timer { get; set; }
        public ScheduledEvent? Ending { get; set; }
    }
}
