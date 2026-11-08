namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 980: "Strike target enemy with your sword, dealing 71% of Strength physical damage to them and nearby enemies.
/// VETERAN OF WAR: Measured Strike reduces the cooldown of your Shield Slam and Shield Throw abilities by 2 seconds." No
/// cooldown; damage falls off as sqrt(3/N) beyond 3 enemies (effect 1273).
/// </summary>
public partial class MeasuredStrike : HelenaAbility
{
    public const double ReductionSeconds = 2.0;

    public MeasuredStrike(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = StrengthCoefficient;
        AoeTargetThreshold = DamageScalingTargetCountThreshold;
        VeteranOfWarReductions.Add((typeof(ShieldSlam), ReductionSeconds));
        VeteranOfWarReductions.Add((typeof(ShieldThrow), ReductionSeconds));
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        DealAoeDamage(ctx, Enemies(ctx, target));
        ApplyVeteranOfWar(ctx);
    }
}
