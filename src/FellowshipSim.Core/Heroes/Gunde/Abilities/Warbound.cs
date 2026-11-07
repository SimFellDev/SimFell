namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2299: "Quickly leap in the direction you are moving, dealing damage to all enemies in your path."
/// It has charges whose recharge is not hasted. The enemies in the path are the target alone in a sim; a replay follows the log.
/// UNVERIFIED: whether it takes the global cooldown.
/// </summary>
public class Warbound : GundeAbility
{
    public const int AbilityId = 2299;
    public const string AbilityName = "Warbound";
    public const double Coefficient = 0.2;
    public const double CooldownSeconds = 8.0;
    public const int Charges = 2;

    public Warbound(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        foreach (var enemy in ctx.Outcomes.SelectTargets(DamageLogId, Single(target), ctx)) DealDamage(ctx, enemy);
    }
}
