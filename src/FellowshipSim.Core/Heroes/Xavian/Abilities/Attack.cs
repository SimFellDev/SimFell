namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1854: Xavian's weapon swing, 98% of Strength (game data coefficient 0.98, spread 0.2). Combat logs record every
/// swing as a cast; an action list uses it as soon as the swing timer allows.
/// </summary>
public partial class Attack : XavianAbility
{
    /// <summary>
    /// Seconds between swings before Haste. The game data has no value (GA_Sune_MeleeAutoAttack ScaleTimeWithAttackSpeed).
    /// Combat logs (XyGpcHjNaAzgk7mW fight 20, about 18% Haste): swings 2.20s apart with no Haste buff, 1.80s during Spirit
    /// of Heroism. UNVERIFIED.
    /// </summary>
    public const double SwingSeconds = 2.6;

    /// <summary>
    /// Chance an enemy avoids a swing (a miss or a dodge, no damage). Combat logs (16 Xavian players of patch 0.5.0.0): 609
    /// of 7,353 swings avoided, 5-11% a fight, and no other Xavian hit. UNVERIFIED estimate of the game's chance.
    /// </summary>
    public const double AvoidChance = 0.083;
    public const string AvoidedProcKey = "xavian_attack_avoided";

    public Attack(Xavian owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(SwingSeconds, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is not { IsSleeping: false }) return;
        // A swing the enemy avoids is a hit of no damage.
        bool avoided = ctx.Outcomes.RollProc(AvoidedProcKey, AvoidChance, ctx, target);
        DealDamage(ctx, target, avoided ? 0.0 : 1.0, canCrit: !avoided);
    }
}
