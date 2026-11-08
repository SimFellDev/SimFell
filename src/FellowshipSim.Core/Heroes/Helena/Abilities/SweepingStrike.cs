namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 981: "Deals 375.6% of Strength physical damage to your target and other nearby enemies and grants you Blood
/// Rally for 6 seconds, healing you for 5% of your maximum Health every second. Enemies hit by Sweeping Strike bleed for
/// 60% of Strength physical damage every 1.5 seconds for 12 seconds." Cooldown 30s; the hits lose damage as sqrt(1/N)
/// beyond one enemy (effect 1274). Blood Rally is effect 3098 and the bleed <see cref="SweepingStrikeBleed"/>.
/// </summary>
public partial class SweepingStrike : HelenaAbility
{
    public const double Coefficient = 3.756;
    public const int BloodRallyEffectId = 3098;
    public const double BloodRallySeconds = 6.0;

    /// <summary>Blood Rally on Helena (effect 3098).</summary>
    public static readonly AuraDef BloodRally = AuraDef.Effect("Sweeping Strike: Blood Rally", BloodRallyEffectId, BloodRallySeconds);

    /// <summary>Blood Rally as it is applied (Lingering Rush lengthens it).</summary>
    public AuraDef BloodRallyBuff { get; set; } = BloodRally;

    private SweepingStrikeBleed? _bleed;

    public SweepingStrike(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(AbilityCooldown);
        AoeTargetThreshold = DamageScalingTargetCountThreshold;
    }

    public override void Connect() => _bleed = Helena.GetAbility<SweepingStrikeBleed>();

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var targets = ctx.Outcomes.SelectTargets(DamageLogId, Enemies(ctx, target), ctx);
        double falloff = targets.Count > DamageScalingTargetCountThreshold
            ? Math.Sqrt((double)DamageScalingTargetCountThreshold / targets.Count)
            : 1.0;
        for (int i = 0; i < targets.Count; i++)
        {
            DealDamage(ctx, targets[i], damageMultiplier: falloff);
            _bleed?.ApplyTo(ctx, targets[i]);
        }
        Helena.Auras.Apply(BloodRallyBuff, ctx.CurrentTime);
    }
}
