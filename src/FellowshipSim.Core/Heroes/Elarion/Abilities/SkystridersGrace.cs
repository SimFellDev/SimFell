namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1304: "You are granted Haste. Can be used during Global Cooldown and while Casting or
/// Channeling." Has a cooldown; effect 1869 (Haste).
/// </summary>
public class SkystridersGrace : ElarionAbility
{
    public const int AbilityId = 1304;
    public const string AbilityName = "Skystrider's Grace";
    public const double CooldownSeconds = 120.0;
    public const int BuffEffectId = 1869;
    public const double BuffSeconds = 20.0;
    public const double BuffHaste = 0.3;

    public static readonly AuraDef Buff = AuraDef.Effect("Skystrider's Grace", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.Add(StatType.Haste, BuffHaste));

    public SkystridersGrace(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
