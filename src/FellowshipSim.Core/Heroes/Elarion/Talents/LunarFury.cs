using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 130: Heartseeker Barrage hits have a multiplied chance to trigger Lunarlight Salvo when dealing damage to
/// enemies with Lunarlight Mark active. Additionally, Lunarlight Salvo deals increased damage. Effects 1877 and
/// 2832 talent IncreasedDamage DamageScale: the eruption too.
/// Combat logs (pWmcV42K39GHTLQX fight 23): the Heartseeker Barrage hits on a marked enemy that do not crit call
/// down a salvo at the multiplied chance, and every crit does.
/// </summary>
public class LunarFury : Talent
{
    public const int TalentId = 130;
    public const string TalentName = "Lunar Fury";
    public const double ChanceMultiplier = 2.0;
    public const double SalvoDamage = 1.3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion elarion) return;
        elarion.Salvo.ChanceMultipliers.Add(id => id == HeartseekerBarrage.AbilityId ? ChanceMultiplier : 1.0);
        elarion.Salvo.AddDamageMultiplier(SalvoDamage);
        elarion.Salvo.Eruption.AddDamageMultiplier(SalvoDamage);
    }
}
