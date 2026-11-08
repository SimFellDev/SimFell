namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1107: "Send a shockwave through the ground in front of you, dealing 106.4% of Agility damage to enemies in its
/// wake and Stunning them for 3 sec. Can be used during Global Cooldown". 60 second cooldown. The damage is effect 1542,
/// logged as such. Stone Stomp is cast on its cooldown alone: combat logs (27 fights) have 54 of its 111 casts with no
/// Combo Builder before them.
/// </summary>
public partial class StoneStomp : MeikoAbility
{
    public const int DamageEffectId = 1542;

    public StoneStomp(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = AgilityCoefficient;
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        DamageLogId = LogIds.Effect(DamageEffectId);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        DealAoeDamage(ctx, ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies);
    }
}
