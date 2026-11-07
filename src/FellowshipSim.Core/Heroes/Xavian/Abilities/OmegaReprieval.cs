namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1861: "Instantly gain 2 stacks of Omega Reprieval, causing your Brilliant Flare to turn into Brilliant Flash
/// with added enhancements when used to consume the 2 stacks. Can be used during Global Cooldown." Cooldown 60s.
/// </summary>
public class OmegaReprieval : XavianAbility
{
    public const int AbilityId = 1861;
    public const string AbilityName = "Omega Reprieval";
    public const double CooldownSeconds = 60.0;
    public const int StacksGranted = 2;

    /// <summary>Effect 2574: 12 seconds, up to 99 stacks.</summary>
    public const int BuffEffectId = 2574;
    public const double BuffSeconds = 12.0;
    public const int BuffMaxStacks = 99;
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds, BuffMaxStacks);

    public OmegaReprieval(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Xavian.Auras.Apply(Buff, ctx.CurrentTime, StacksGranted);
    }
}
