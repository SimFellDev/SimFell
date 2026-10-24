namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1234: "Swing your hammer in a wide arc toward target enemy dealing physical damage to it and any nearby
/// enemies." Recklessness: it cannot critically strike, and its damage is increased by your critical strike chance.
/// Generates 3 Fury.
/// </summary>
public class WildSwing : TariqAbility
{
    public const int AbilityId = 1234;
    public const string AbilityName = "Wild Swing";
    /// <summary>Placeholder WildSwingDmg: 129.14% of Strength.</summary>
    public const double Coefficient = 1.2914;
    /// <summary>Game data cooldown 1.5; haste shortens it, as it does Skull Crusher's.</summary>
    public const double CooldownSeconds = 1.5;
    /// <summary>Ink.CleaveAttack.ResourceGain: 3% of maximum Fury.</summary>
    public const double FuryGain = 3.0;
    /// <summary>Game data targetCountFalloff: effect 1770, threshold 5.</summary>
    public const double TargetThreshold = 5;
    /// <summary>
    /// "Enemies have an increased chance to Dodge Wild Swing" (placeholder constant Tariq.DodgeChanceIncreaseForMeleeAttacks
    /// 0.03 names no total). Combat logs (3 Tariq fights): 27 of 388 Wild Swing hits dodged (3-10% a fight), and no other
    /// Tariq hit. UNVERIFIED estimate: the share of all three.
    /// </summary>
    public const double DodgeChance = 0.07;
    public const string DodgeProcKey = "wild_swing_dodged";

    public WildSwing(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Tariq.GainFury(FuryGain, ctx, target);
        // Combat logs (3 Tariq fights, 388 hits): no Wild Swing hit crits.
        double recklessness = 1.0 + Owner.CritChance(ctx.CurrentTime);
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, AreaAround(ctx, target), ctx);
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            // A dodged swing is a hit of no damage.
            bool dodged = ctx.Outcomes.RollProc(DodgeProcKey, DodgeChance, ctx, struck[i]);
            DealDamage(ctx, struck[i], dodged ? 0.0 : recklessness * falloff, canCrit: false);
        }
    }

    /// <summary>The target and the enemies near it (every enemy in a sim).</summary>
    internal static IReadOnlyList<EnemyTarget> AreaAround(SimulationContext ctx, EnemyTarget? target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
