namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2300 / effect 3247: "You take reduced damage for a time. Can be used during Global Cooldown." It has no
/// effect on damage dealt.
/// </summary>
public partial class RecklessAbandon : GundeAbility
{
    public const int BuffEffectId = 3247;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, AbilityDuration);

    public RecklessAbandon(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Gunde.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
