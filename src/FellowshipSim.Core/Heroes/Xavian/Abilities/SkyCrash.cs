namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1841: "Instantly ascend to the skies in a beam of light and crash down at the targeted location, dealing
/// magical damage to enemies in the area. Can be used during Global Cooldown." Cooldown 60s, 2 charges.
/// </summary>
public partial class SkyCrash : XavianAbility
{
    /// <summary>Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 10.051): the crash deals its damage 0.17s after the cast. UNVERIFIED.</summary>
    public const double DamageDelaySeconds = 0.17;

    /// <summary>Raised as the crash deals its damage (Tempered Descent, Zenith Technique).</summary>
    public event Action<SimulationContext>? Crashed;

    public SkyCrash(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.AoeDamageStrengthCoefficient;
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
        AoeTargetThreshold = Constants.AoeDamageScalingTargetCountThreshold;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        ctx.Schedule(SimTime.FromSeconds(DamageDelaySeconds), c =>
        {
            if (c.ActiveEnemies.Count > 0) DealAoeDamage(c, c.ActiveEnemies);
            Crashed?.Invoke(c);
        }, AbilityName);
    }
}
