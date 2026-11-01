namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1123: "Quickly travel forward a short distance. Can be used during Global Cooldown". 20 second cooldown, 2
/// charges. Movement is not simulated.
/// </summary>
public class Gust : MeikoAbility
{
    public const int AbilityId = 1123;
    public const string AbilityName = "Gust";
    public const double CooldownSeconds = 20.0;
    public const int Charges = 2;

    /// <summary>Gust was cast (Slipstream).</summary>
    public event Action<SimulationContext>? Dashed;

    public Gust(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Dashed?.Invoke(ctx);
    }
}
