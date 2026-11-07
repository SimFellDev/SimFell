namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1857: "Rupture the ground in front of you, dealing magical damage to all enemies in a cone in front of you,
/// Stunning them for 2 seconds and Pulling them to you. Can be used during Global Cooldown." Cooldown 45s.
/// </summary>
public class RupturedDawn : XavianAbility
{
    public const int AbilityId = 1857;
    public const string AbilityName = "Ruptured Dawn";
    /// <summary>Placeholder RisingSunDmg: 90% of Strength.</summary>
    public const double Coefficient = 0.9;
    public const double CooldownSeconds = 45.0;
    /// <summary>Game data targetCountFalloff: effect 2567, threshold 8; maxTargets 40.</summary>
    public const double TargetThreshold = 8;

    public RupturedDawn(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        AoeTargetThreshold = TargetThreshold;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var enemies = Area(ctx, target);
        DealAoeDamage(ctx, enemies);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
