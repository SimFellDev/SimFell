namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1095: "Taunt the target, forcing it to attack you for 6 sec. Can be used during Global Cooldown". 8 second
/// cooldown, 2 charges. Effect 1514 (Taunted) is on the enemy. Enemy attacks are not simulated.
/// </summary>
public class Taunt : MeikoAbility
{
    public const int AbilityId = 1095;
    public const string AbilityName = "Taunt";
    public const double CooldownSeconds = 8.0;
    public const int Charges = 2;
    public const int TauntedEffectId = 1514;
    public const double TauntedSeconds = 6.0;

    public static readonly AuraDef Taunted = AuraDef.Effect("Taunted", TauntedEffectId, TauntedSeconds);

    public Taunt(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is { IsSleeping: false }) target.Auras.Apply(Taunted, ctx.CurrentTime);
    }
}
