namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2290: "Jump into a spinning strike, dealing 203.17% of Strength to all nearby enemies. Applies Rend to the
/// targets for 50% of the damage dealt." Damage falls off as sqrt(8/N) beyond 8 enemies (game data effect 3235).
/// Cooldown 5s, shortened by Haste at the cast. The game data lists no Critical Strike for it; combat logs show it crit.
/// </summary>
public class ReaversEdge : GundeAbility
{
    public const int AbilityId = 2290;
    public const string AbilityName = "Reaver's Edge";
    public const double Coefficient = 2.0317;
    public const double CooldownSeconds = 5.0;
    public const double TargetThreshold = 8;
    public const double RendShareOfDamage = 0.5;

    public ReaversEdge(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
        RendShare = RendShareOfDamage;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        RendingPulse(ctx, BeginRend(ctx), AreaTargets(ctx, target));
    }
}
