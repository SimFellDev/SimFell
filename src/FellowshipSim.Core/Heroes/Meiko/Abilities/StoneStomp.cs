namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1107: "Send a shockwave through the ground in front of you, dealing 106.4% of Agility damage to enemies in its
/// wake and Stunning them for 3 sec. Can be used during Global Cooldown". 60 second cooldown. The damage is effect 1542,
/// logged as such; the stun is effect 1538. The game data gives it a cost of 2 CP, but combat logs (27 fights) have 54 of
/// its 111 casts with no Combo Builder before them, so the engine takes no combo for it.
/// </summary>
public class StoneStomp : MeikoAbility
{
    public const int AbilityId = 1107;
    public const string AbilityName = "Stone Stomp";
    /// <summary>Placeholder StoneStompDmg: 106.4% of Agility.</summary>
    public const double Coefficient = 1.064;
    public const double CooldownSeconds = 60.0;
    public const int DamageEffectId = 1542;
    public const int StunEffectId = 1538;
    public const double StunSeconds = 3.0;

    public static readonly AuraDef Stunned = AuraDef.Effect("Stunned", StunEffectId, StunSeconds);

    public StoneStomp(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        DamageLogId = LogIds.Effect(DamageEffectId);
        Hit += (in hit) => hit.Target?.Auras.Apply(Stunned, hit.Timestamp);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        DealAoeDamage(ctx, ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies);
    }
}
