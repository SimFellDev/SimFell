using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 130: Heartseeker Barrage hits have a multiplied chance to trigger Lunarlight Salvo when dealing damage to
/// enemies with Lunarlight Mark active. Additionally, Lunarlight Salvo deals increased damage. Effects 1877 and
/// 2832 talent IncreasedDamage DamageScale: the eruption too.
/// Combat logs (pWmcV42K39GHTLQX fight 23): the Heartseeker Barrage hits on a marked enemy that do not crit call
/// down a salvo at the multiplied chance, and every crit does.
/// </summary>
public partial class LunarFury : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion elarion) return;
        elarion.Salvo.ChanceMultipliers.Add(id => id == HeartseekerBarrage.AbilityId ? IncreasedInstantMarkTriggerChanceChanceMultiplier : 1.0);
        elarion.Salvo.AddDamageMultiplier(IncreasedDamageDamageScale);
        elarion.Salvo.Eruption.AddDamageMultiplier(IncreasedDamageDamageScale);
    }
}
