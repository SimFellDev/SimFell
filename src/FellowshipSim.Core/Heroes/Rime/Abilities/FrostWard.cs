namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1015: reduces damage you take. Can be used during Global Cooldown. Effect
/// 1379 "Frost Ward", an IncomingDamageMultiplier (game data). No damage.
/// </summary>
public partial class FrostWard : RimeAbility
{
    public const int BuffEffectId = 1379;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, AbilityDuration,
        modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, Constants.IncomingDamageScaler));

    public FrostWard(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }
}
