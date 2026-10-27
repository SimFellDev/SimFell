namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 161: "Conjure the forces of nature to bounce between up to 4 enemies, dealing 1397.7% Main Stat nature
/// damage to each one ... The 1st hit from Nature's Fury has +100% increased damage or healing. Nature's Fury has an
/// additional +30% Critical Strike chance." Cast 1.5s (hasted), cooldown 60s (datamine export 26136736). Combat logs
/// (JW7pzaBxMd9bNCZK fight 112 and jHDC613ZzbTYR8wW fight 21, two Vigour players): the first hit lands as the cast ends and
/// each bounce 0.3s after the one before; a bounce can strike an enemy already hit. UNVERIFIED: no Rime log has it; the
/// game's damage spread for it is 20%, the engine's usual one is used.
/// </summary>
public class NaturesFury : Ability
{
    public const int AbilityId = 161;
    public const string AbilityName = "Nature's Fury";
    public const double Coefficient = 13.977;
    public const double CooldownSeconds = 60.0;
    public const double CastTimeSeconds = 1.5;
    public const int Hits = 4;
    public const double BounceSeconds = 0.3;
    public const double FirstHitMultiplier = 2.0;
    public const double AddedCritChance = 0.3;

    public NaturesFury(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds);
        CritChanceBonus = AddedCritChance;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        // The first hit lands right after the cast completes (not inside it), then each bounce.
        ctx.Schedule(SimTime.Zero, c => DealDamage(c, target, FirstHitMultiplier), Name);
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
