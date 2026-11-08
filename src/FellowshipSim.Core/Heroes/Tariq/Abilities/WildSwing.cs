namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1234: "Swing your hammer in a wide arc toward target enemy dealing physical damage to it and any nearby
/// enemies." Recklessness: it cannot critically strike, and its damage is increased by your critical strike chance.
/// Generates Fury.
/// </summary>
public partial class WildSwing : TariqAbility
{
    /// <summary>
    /// <summary>Game data: a share of maximum Fury.</summary>
    public const double FuryGain = 3.0;
    /// <summary>
    /// "Enemies have an increased chance to Dodge Wild Swing" (the tooltip
    /// names no total). Combat logs (3 Tariq fights): avoided Wild Swing hits are misses and dodges, and no other Tariq
    /// hit is avoided. UNVERIFIED estimate: the share of all three, misses and dodges as one.
    /// </summary>
    public const double AvoidChance = 0.07;
    public const string AvoidedProcKey = "wild_swing_avoided";

    public WildSwing(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = StrengthCoefficient;
        // Game data: no cooldown effect, so the cooldown is its global cooldown, 1 / Haste snapshotted at commit, outside
        // Cooldown Recovery.
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        IgnoresCooldownRecovery = true;
        AoeTargetThreshold = TargetThresholdForDamageScale;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Tariq.GainFury(FuryGain, ctx, target);
        // Combat logs (3 Tariq fights, 388 hits): no Wild Swing hit crits.
        double recklessness = 1.0 + Owner.CritChance(ctx.CurrentTime);
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, AreaAround(ctx, target), ctx);
        double falloff = struck.Count > TargetThresholdForDamageScale ? Math.Sqrt((double)TargetThresholdForDamageScale / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            // A swing the enemy avoids is a hit of no damage.
            bool avoided = ctx.Outcomes.RollProc(AvoidedProcKey, AvoidChance, ctx, struck[i]);
            DealDamage(ctx, struck[i], avoided ? 0.0 : recklessness * falloff, canCrit: false);
        }
    }

    /// <summary>The target and the enemies near it (every enemy in a sim).</summary>
    internal static IReadOnlyList<EnemyTarget> AreaAround(SimulationContext ctx, EnemyTarget? target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
