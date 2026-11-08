namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1861: "Instantly gain 2 stacks of Omega Reprieval, causing your Brilliant Flare to turn into Brilliant Flash
/// with added enhancements when used to consume the 2 stacks. Can be used during Global Cooldown." Cooldown 60s.
/// </summary>
public partial class OmegaReprieval : XavianAbility
{
    /// <summary>Effect 2574: 12 seconds, up to 99 stacks.</summary>
    public const int BuffEffectId = 2574;
    public const int BuffMaxStacks = 99;
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffDuration, BuffMaxStacks);

    public OmegaReprieval(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Xavian.Auras.Apply(Buff, ctx.CurrentTime, Stacks);
    }
}
