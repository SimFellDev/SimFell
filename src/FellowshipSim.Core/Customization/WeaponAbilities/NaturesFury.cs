namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 161: "Conjure the forces of nature to bounce between enemies, dealing Main Stat nature
/// damage to each one ... The 1st hit from Nature's Fury has increased damage or healing. Nature's Fury has
/// additional Critical Strike chance." Hasted cast (game data). Combat logs
/// (JW7pzaBxMd9bNCZK fight 112 and jHDC613ZzbTYR8wW fight 21, two Vigour players): the first hit lands as the cast ends and
/// each bounce a fixed time after the one before; a bounce can strike an enemy already hit. UNVERIFIED: no Rime log has it; the
/// game's damage spread for it differs from the engine's usual one, which is used.
/// </summary>
public partial class NaturesFury : Ability
{
    /// <summary>Game data: tuning <c>Damage.PrimaryAttributeCoefficient</c> of weapon power 356.</summary>
    public const double Coefficient = 13.977;
    /// <summary>Game data: tuning <c>MaxEnemyTargets</c> of ability 13, the bounce ability weapon power 356 grants.</summary>
    public const int Hits = 4;
    /// <summary>Combat logs: each bounce lands 0.3s after the one before (ability 13's <c>JumpDelay</c> is 0.15).</summary>
    public const double BounceSeconds = 0.3;

    public NaturesFury(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        SetCooldown(AbilityCooldown);
        CritChanceBonus = AbilityAddedCritChance;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        // The first hit lands right after the cast completes (not inside it), then each bounce.
        ctx.Schedule(SimTime.Zero, c => DealDamage(c, target, InitialHitPowerScaler), Name);
        for (int i = 1; i < Hits; i++)
        {
            ctx.Schedule(SimTime.FromSeconds(i * BounceSeconds), c =>
            {
                IReadOnlyList<EnemyTarget> enemies = c.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : c.ActiveEnemies;
                if (c.Outcomes.ChooseTarget(DamageLogId, enemies, c) is { } next) DealDamage(c, next);
            }, Name);
        }
    }
}
