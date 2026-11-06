namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1015: reduces damage you take. Can be used during Global Cooldown. Effect
/// 1379 "Frost Ward", an IncomingDamageMultiplier (game data). No damage.
/// </summary>
public class FrostWard : RimeAbility
{
    public const int AbilityId = 1015;
    public const string AbilityName = "Frost Ward";
    public const double CooldownSeconds = 30.0;
    public const int BuffEffectId = 1379;
    public const double BuffSeconds = 4.0;
    public const double IncomingDamage = 0.6;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, IncomingDamage));

    public FrostWard(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Defensive)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }
}
