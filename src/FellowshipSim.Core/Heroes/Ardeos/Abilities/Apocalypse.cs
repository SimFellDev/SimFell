namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1273: a cast that conjures a major eruption of fire in the targeted area, dealing Intellect fire damage and
/// applying Searing Blaze to all enemies in a large radius. Damage falls off with the target count (game data
/// targetCountFalloff, effect 1833).
/// </summary>
public partial class Apocalypse : ArdeosAbility
{
    /// <summary>Seconds taken off the cast time (Apocalyptic Surge).</summary>
    public double CastTimeReductionSeconds { get; set; }

    public Apocalypse(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = IntellectCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        SetCooldown(AbilityCooldown);
        AoeTargetThreshold = DamageScalingTargetCountThreshold;
        // Every enemy the eruption hits gets Searing Blaze.
        Hit += (in DamageContext hit) =>
        {
            if (hit.Target != null && hit.SimContext != null && _searingBlaze != null) owner.Dots.Apply(hit.SimContext, _searingBlaze, hit.Target);
        };
    }

    /// <summary>Gives Apocalypse more charges (Explosivo).</summary>
    public void AddCharges(int charges) => SetCooldown(AbilityCooldown, MaxCharges + charges);

    private SearingBlazeDot? _searingBlaze;

    public override void Connect() => _searingBlaze = Ardeos.GetAbility<SearingBlaze>()?.Dot;

    public override SimTime GetAdjustedCastTime(SimTime now = default) =>
        SimTime.FromSeconds(Math.Max(0.0, CastTime.TotalSeconds - CastTimeReductionSeconds) / (1.0 + Owner.GetHaste(now)));

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target != null ? [target] : ctx.ActiveEnemies;
        DealAoeDamage(ctx, enemies);
    }
}
