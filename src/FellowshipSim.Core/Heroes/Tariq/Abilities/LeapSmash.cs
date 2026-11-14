namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1225: "Instantly Leap to target area and smash the ground, dealing physical damage to all nearby enemies. Can
/// be used during Global Cooldown."
/// </summary>
public partial class LeapSmash : TariqAbility
{
    /// <summary>
    /// Tooltip value. Combat logs (3 Tariq fights, 896 hits): Leap
    /// Smash crits more often than Face Breaker, and grievous crits come with it.
    /// </summary>
    public const double AddedCritChance = 0.40;
    /// <summary>Combat logs (3 Tariq fights, 170 casts): the smash deals its damage a fixed delay after the cast, and Mouth for War's stack comes with it.</summary>
    public const double LandingSeconds = 0.30;

    /// <summary>A cast landed, with the enemies it hit (High Road, Mouth for War, Slayer's Mosh).</summary>
    public event Action<SimulationContext, IReadOnlyList<EnemyTarget>>? Smashed;

    public LeapSmash(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        AoeTargetThreshold = Constants.TargetThresholdForDamageScale;
        CritChanceBonus = AddedCritChance;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        ctx.Schedule(SimTime.FromSeconds(LandingSeconds), c =>
        {
            var struck = c.Outcomes.SelectTargets(DamageLogId, WildSwing.AreaAround(c, target), c);
            DealToEach(c, struck, Constants.TargetThresholdForDamageScale);
            Smashed?.Invoke(c, struck);
        }, Name);
    }
}
