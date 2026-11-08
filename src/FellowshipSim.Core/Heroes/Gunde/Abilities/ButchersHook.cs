namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2284: "Throw a chained axe at target enemy, dealing damage and pulling them to you. Can be used during
/// Global Cooldown." UNVERIFIED: the game data's text has a Rend share placeholder, but no log shows it applying
/// Rend.
/// </summary>
public partial class ButchersHook : GundeAbility
{

    public ButchersHook(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = StrengthCoefficient;
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is { IsSleeping: false }) DealDamage(ctx, target);
    }
}
