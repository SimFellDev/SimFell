namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 2233 (talent Macabre Stratagem): gains Matriarch Macabre. If used while Matriarch Macabre
/// is already active, its duration is extended. It grants no Spirit of Heroism.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 28.015): Matriarch Macabre (effect 2039) applies at the cast.
/// </summary>
public partial class MacabreStratagem : MaraAbility
{
    public MacabreStratagem(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (!Mara.HasMacabreStratagem)
        {
            reason = explain ? $"{Name} needs the Macabre Stratagem talent" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        TriggerCooldown(ctx);
        if (Mara.Auras.HasAura(MatriarchMacabre.Buff, now)) Mara.Auras.Extend(MatriarchMacabre.Buff, SimTime.FromSeconds(AbilityDuration));
        else Mara.Auras.Apply(MatriarchMacabre.Buff.WithDuration(AbilityDuration), now);
    }
}
