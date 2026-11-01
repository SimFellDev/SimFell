namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1120, a Palm Technique finisher (Spirit Palm then Earth Fist): "Meiko creates a vortex of winds around her,
/// providing +15% Dodge Chance and dealing 105.56% of Agility damage every 2 seconds for 30 seconds to all nearby enemies.
/// Enemies that take damage from Spirited Vortex have 35% reduced Movement Speed and 10% reduced Damage for 5 seconds. Can
/// be used during Global Cooldown". Logged under 1120. Dodge is not simulated.
/// </summary>
public class SpiritedVortex : MeikoFinisher
{
    public const int AbilityId = 1120;
    public const string AbilityName = "Spirited Vortex";
    /// <summary>Placeholder SpiritVortexDmg: 105.56% of Agility.</summary>
    public const double Coefficient = 1.0556;
    public const double DurationSeconds = 30.0;
    /// <summary>
    /// SpiritVortexPeriod 2, hasted: combat logs (27 fights) have a hit on every enemy at the cast, then every 1.17-1.78s
    /// with the Haste of the moment.
    /// </summary>
    public const double PeriodSeconds = 2.0;
    /// <summary>Game data targetCountFalloff threshold 8, maxTargets 40.</summary>
    public const double TargetThreshold = 8;
    /// <summary>Effect 1570: the vortex on Meiko (DodgeChance).</summary>
    public const int BuffEffectId = 1570;
    /// <summary>Effect 1568: 35% reduced Movement Speed for 5 seconds on each enemy struck.</summary>
    public const int SlowEffectId = 1568;
    /// <summary>Effect 2487: 10% reduced damage for 5 seconds on each enemy struck.</summary>
    public const int EnfeebledEffectId = 2487;
    public const double DebuffSeconds = 5.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Spirited Vortex", BuffEffectId, DurationSeconds);
    public static readonly AuraDef Slow = AuraDef.Effect("Spirited Vortex Slow", SlowEffectId, DebuffSeconds);
    public static readonly AuraDef Enfeebled = AuraDef.Effect("Enfeebled", EnfeebledEffectId, DebuffSeconds);

    private PeriodicEffect? _vortex;
    private double _vortexMultiplier = 1.0;

    public SpiritedVortex(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core, Technique.Palm, Technique.Fist)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        Hit += (in hit) =>
        {
            if (hit.Target is not { } enemy) return;
            enemy.Auras.Apply(Slow, hit.Timestamp);
            enemy.Auras.Apply(Enfeebled, hit.Timestamp);
        };
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target) => Activate(ctx, target, CastDamageMultiplier);

    /// <summary>
    /// Starts a vortex around Meiko with <paramref name="damageMultiplier"/> on its hits, ending the one before (the
    /// finisher, and Will of Stone's Shatter Earth).
    /// </summary>
    public void Activate(SimulationContext ctx, EnemyTarget target, double damageMultiplier = 1.0)
    {
        _vortex?.Stop(ctx);
        _vortexMultiplier = damageMultiplier;
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
        _vortex = PeriodicEffect.BeginHasted(ctx, Owner, Name, DurationSeconds, PeriodSeconds, tickOnStart: true, partialFinalTick: false,
            onTick: (c, _) => DealAoeDamage(c, c.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : c.ActiveEnemies, _vortexMultiplier),
            damageLogId: DamageLogId);
    }
}
