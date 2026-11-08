namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1304: "You are granted Haste. Can be used during Global Cooldown and while Casting or
/// Channeling." Has a cooldown; effect 1869 (Haste).
/// </summary>
public partial class SkystridersGrace : ElarionAbility
{
    public const int BuffEffectId = 1869;

    public static readonly AuraDef Buff = AuraDef.Effect("Skystrider's Grace", BuffEffectId, AbilityDuration,
        modifiers: StatModifier.Add(StatType.Haste, HasteIncrease));

    public SkystridersGrace(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
