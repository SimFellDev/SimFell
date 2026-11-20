namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1120, a Palm Technique finisher (Spirit Palm then Earth Fist): "Meiko creates a vortex of winds around her,
/// providing +15% Dodge Chance and dealing 105.56% of Agility damage every 2 seconds for 30 seconds to all nearby enemies.
/// Enemies that take damage from Spirited Vortex have 35% reduced Movement Speed and 10% reduced Damage for 5 seconds. Can
/// be used during Global Cooldown". Logged under 1120. Effect 1570 on Meiko marks the vortex's time.
/// </summary>
public partial class SpiritedVortex : MeikoFinisher
{
    /// <summary>Effect 1570: the vortex on Meiko.</summary>
    public const int BuffEffectId = 1570;

    public static readonly AuraDef Buff = AuraDef.Effect("Spirited Vortex", BuffEffectId, AbilityDuration);

    private PeriodicEffect? _vortex;
    private SimTime _vortexStart;
    private double _vortexMultiplier = 1.0;

    public SpiritedVortex(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory, Technique.Palm, Technique.Fist)
    {
        BaseCoefficient = Constants.AgilityCoefficient;
        AoeTargetThreshold = Constants.TargetCountDamageScalingThreshold;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target) => Activate(ctx, target, CastDamageMultiplier);

    /// <summary>
    /// Starts a vortex around Meiko with <paramref name="damageMultiplier"/> on its hits, ending the one before (the
    /// finisher, and Will of Stone's Shatter Earth). <see cref="AbilityPeriod"/> is hasted: combat logs (27 fights) have a hit
    /// on every enemy at the cast, then every 1.17-1.78s with the Haste of the moment.
    /// </summary>
    public void Activate(SimulationContext ctx, EnemyTarget target, double damageMultiplier = 1.0)
    {
        // A vortex of an earlier iteration (its clock past this one's) is gone with that iteration.
        if (_vortex is { IsActive: true } && _vortexStart <= ctx.CurrentTime) _vortex.Stop(ctx);
        _vortexMultiplier = damageMultiplier;
        _vortexStart = ctx.CurrentTime;
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
        _vortex = PeriodicEffect.BeginHasted(ctx, Owner, Name, AbilityDuration, AbilityPeriod, tickOnStart: true, partialFinalTick: false,
            onTick: (c, _) => DealAoeDamage(c, c.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : c.ActiveEnemies, _vortexMultiplier),
            damageLogId: DamageLogId);
    }
}
