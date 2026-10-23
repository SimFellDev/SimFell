namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1225: "Instantly Leap to target area and smash the ground, dealing physical damage to all nearby enemies. Can
/// be used during Global Cooldown."
/// </summary>
public class LeapSmash : TariqAbility
{
    public const int AbilityId = 1225;
    public const string AbilityName = "Leap Smash";
    /// <summary>Placeholder LeapSmashDmg: 69% of Strength.</summary>
    public const double Coefficient = 0.69;
    /// <summary>Game data cooldown 20, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 20.0;
    /// <summary>Game data targetCountFalloff: effect 1757, threshold 8.</summary>
    public const double TargetThreshold = 8;
    /// <summary>
    /// Placeholder constant Talent.IncreasedCriticalChance.AddedCrit 0.4. Combat logs (3 Tariq fights, 896 hits): Leap
    /// Smash crits 72-86% of the time against 35-44% for Face Breaker, and grievous crits come with it.
    /// </summary>
    public const double AddedCritChance = 0.40;
    /// <summary>Combat logs (3 Tariq fights, 170 casts): the smash lands 0.27-0.30s after the cast; Mouth for War's stack comes with it.</summary>
    public const double LandingSeconds = 0.30;

    /// <summary>A cast landed, with the enemies it hit (High Road, Mouth for War, Slayer's Mosh).</summary>
    public event Action<SimulationContext, IReadOnlyList<EnemyTarget>>? Smashed;

    public LeapSmash(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
        CritChanceBonus = AddedCritChance;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        ctx.Schedule(SimTime.FromSeconds(LandingSeconds), c =>
        {
            var struck = c.Outcomes.SelectTargets(DamageLogId, WildSwing.AreaAround(c, target), c);
            DealToEach(c, struck, TargetThreshold);
            Smashed?.Invoke(c, struck);
        }, Name);
    }
}
