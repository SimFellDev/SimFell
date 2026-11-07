namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2300 / effect 3247: "You take reduced damage for a time. Can be used during Global Cooldown." It has no
/// effect on damage dealt.
/// </summary>
public class RecklessAbandon : GundeAbility
{
    public const int AbilityId = 2300;
    public const string AbilityName = "Reckless Abandon";
    public const double CooldownSeconds = 30.0;
    public const int BuffEffectId = 3247;
    public const double BuffSeconds = 4.0;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public RecklessAbandon(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Defensive)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Gunde.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
