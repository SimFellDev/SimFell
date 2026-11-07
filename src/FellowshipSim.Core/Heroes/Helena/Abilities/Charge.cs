namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 974: "Charge at target enemy, dealing 361.7% of Strength physical damage and Stunning them for 2 seconds."
/// Cooldown 20s. The game data gives it no global cooldown: combat logs (18 fights) have it cast up to 1.1s into one.
/// </summary>
public class Charge : HelenaAbility
{
    public const int AbilityId = 974;
    public const string AbilityName = "Charge";
    public const double Coefficient = 3.617;
    public const double CooldownSeconds = 20.0;

    /// <summary>Raised as the charge strikes its enemy (Vanguard's Advance).</summary>
    public event Action<SimulationContext, EnemyTarget>? Impact;

    public Charge(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is not { IsSleeping: false }) return;
        DealDamage(ctx, target);
        Impact?.Invoke(ctx, target);
    }
}
