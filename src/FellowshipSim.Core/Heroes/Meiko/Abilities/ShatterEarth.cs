namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1115: "Leap and slam the ground at a target location, dealing 746% of Agility damage to all enemies in the area
/// and leaving an area of quaking earth for 6 seconds that deals 142% of Agility damage every 1 seconds to any enemy
/// standing in in the effect." 45 second cooldown, on the global cooldown. The slam is effect 1557, logged under 1115;
/// the quake is <see cref="ShatterEarthQuake"/>.
/// </summary>
public partial class ShatterEarth : MeikoAbility
{
    /// <summary>Placeholder ShatterEarthAoeDmg: 746% of Agility.</summary>
    public const double Coefficient = 7.46;
    /// <summary>Combat logs (27 fights): the slam lands 0.27-0.37 seconds after the cast.</summary>
    public const double LeapSeconds = 0.3;

    /// <summary>Shatter Earth was cast (Earthbourne, Will of Stone).</summary>
    public event Action<SimulationContext, EnemyTarget>? Slammed;

    public ShatterEarth(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(AbilityCooldown);
        AoeTargetThreshold = TargetCountDamageScalingThreshold;
        Quake = new ShatterEarthQuake(owner);
    }

    /// <summary>The quaking earth the slam leaves.</summary>
    public ShatterEarthQuake Quake { get; }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Slammed?.Invoke(ctx, target);
        ctx.Schedule(SimTime.FromSeconds(LeapSeconds), c =>
        {
            var struck = c.Outcomes.SelectTargets(DamageLogId, c.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : c.ActiveEnemies, c);
            double falloff = struck.Count > AoeTargetThreshold ? Math.Sqrt(AoeTargetThreshold / struck.Count) : 1.0;
            for (int i = 0; i < struck.Count; i++) DealDamage(c, struck[i], falloff);
            Quake.Begin(c, struck);
        }, AbilityName);
    }
}
