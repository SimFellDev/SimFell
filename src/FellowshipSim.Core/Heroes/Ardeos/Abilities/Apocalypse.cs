namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1273: 3s cast, 45s. "Conjure a major eruption of fire in the targeted area, dealing 2407% of Intellect fire
/// damage and applying Searing Blaze to all enemies in a large radius." Damage falls off as sqrt(1/N) beyond 1 target
/// (game data targetCountFalloff, effect 1833).
/// </summary>
public class Apocalypse : ArdeosAbility
{
    public const int AbilityId = 1273;
    public const string AbilityName = "Apocalypse";
    public const double Coefficient = 24.07;
    public const double CastTimeSeconds = 3.0;
    public const double CooldownSeconds = 45.0;
    public const double TargetThreshold = 1;

    /// <summary>Seconds taken off the cast time (Apocalyptic Surge).</summary>
    public double CastTimeReductionSeconds { get; set; }

    public Apocalypse(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds);
        AoeTargetThreshold = TargetThreshold;
        // Every enemy the eruption hits gets Searing Blaze.
        Hit += (in DamageContext hit) =>
        {
            if (hit.Target != null && hit.SimContext != null && _searingBlaze != null) owner.Dots.Apply(hit.SimContext, _searingBlaze, hit.Target);
        };
    }

    /// <summary>Gives Apocalypse more charges (Explosivo).</summary>
    public void AddCharges(int charges) => SetCooldown(CooldownSeconds, MaxCharges + charges);

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
