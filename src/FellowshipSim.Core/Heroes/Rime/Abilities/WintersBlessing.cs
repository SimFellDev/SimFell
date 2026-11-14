using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1026 / effects 1389 + 2299: increases Spirit. Usable during the global cooldown. Effect 2299 has the Spirit
/// bonus; combat logs show effect 1389 alone.
/// </summary>
public partial class WintersBlessing : RimeAbility
{
    public const int BuffEffectId = 1389;

    public static readonly AuraDef Buff = AuraDef.Effect("Winter's Blessing", BuffEffectId, AbilityDuration,
        modifiers: StatModifier.Add(StatType.Spirit, Constants.SpiritIncrease));

    public WintersBlessing(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
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
