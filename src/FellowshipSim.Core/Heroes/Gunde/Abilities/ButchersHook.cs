namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2284: "Throw a chained axe at target enemy, dealing 37% of Strength and pulling them to you. Can be used during
/// Global Cooldown." Cooldown 30s. UNVERIFIED: the game data's text has a 50% placeholder, but no log shows it applying
/// Rend.
/// </summary>
public class ButchersHook : GundeAbility
{
    public const int AbilityId = 2284;
    public const string AbilityName = "Butcher's Hook";
    public const double Coefficient = 0.37;
    public const double CooldownSeconds = 30.0;

    public ButchersHook(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is { IsSleeping: false }) DealDamage(ctx, target);
    }
}
