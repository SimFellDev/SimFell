namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1021: blasts enemies in front of you with arctic winds, knocking them back and slowing them. Can be used during
/// Global Cooldown. Effect 1385 "Frigid Winds", a MoveSpeed multiplier (game data). No damage. The enemies in front are the target alone.
/// </summary>
public partial class FrigidWinds : RimeAbility
{
    public const int DebuffEffectId = 1385;

    public static readonly AuraDef Debuff = AuraDef.Effect(AbilityName, DebuffEffectId, DebuffDuration);

    public FrigidWinds(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        target?.Auras.Apply(Debuff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }
}
