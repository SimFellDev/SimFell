namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1260: instant, with charges. Throws a ball of fire at the targeted area, dealing Intellect fire damage to all
/// enemies in the radius and setting them on fire (<see cref="FireBallDot"/>). The hero's Haste is also applied as
/// Cooldown Acceleration on Fire Ball. Damage falls off with the target count (game data targetCountFalloff).
/// </summary>
public class FireBall : ArdeosAbility
{
    public const int AbilityId = 1260;
    public const string AbilityName = "Fire Ball";
    public const double Coefficient = 5.8482;
    public const double CooldownSeconds = 30.0;
    public const int Charges = 2;
    public const double TargetThreshold = 5;
    /// <summary>The additional share of the initial damage dealt over the effect's duration.</summary>
    public const double DotFractionOfHit = 0.20;
    /// <summary>Combat logs (yMAZVJL9hTpFjqbC fight 7, 40 casts): the ball strikes a fixed delay after the cast.</summary>
    public const double ImpactDelaySeconds = 0.97;

    public FireBallDot Dot { get; }

    /// <summary>Share of each hit added to its damage over time (Great Balls of Fire adds to it).</summary>
    public double DotFraction { get; set; } = DotFractionOfHit;

    public FireBall(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
        Dot = new FireBallDot(owner);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        // What talents added to the cast (Reign of Fire's crit) goes with the ball.
        double castCrit = CurrentCast?.CritChance ?? 0.0;
        double castDamage = CastDamageMultiplier;
        ctx.Schedule(SimTime.FromSeconds(ImpactDelaySeconds), c => Strike(c, target, castCrit, castDamage), Name);
    }

    /// <summary>The ball strikes the enemies present now; its target falloff also scales the Cinders of their burns.</summary>
    private void Strike(SimulationContext ctx, EnemyTarget target, double castCrit, double castDamage)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        if (struck.Count == 0) return;
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        foreach (var enemy in struck)
        {
            DealDamage(ctx, enemy, damageMultiplier: falloff * castDamage, extraCritChance: castCrit, onLanded: (c, dealt) =>
            {
                if (dealt > 0) Ardeos.Dots.Apply(c, Dot, enemy, pool: dealt * DotFraction, cinderScale: falloff);
            });
        }
    }
}
