using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 130: "Your Heartseeker Barrage hits have twice the chance to trigger Lunarlight Salvo when dealing damage to
/// enemies with Lunarlight Mark active. Additionally, your Lunarlight Salvo deals +30% increased damage." Effects 1877 and
/// 2832 talent IncreasedDamage DamageScale 1.3: the eruption too.
/// Combat logs (pWmcV42K39GHTLQX fight 23): 50% of Heartseeker Barrage's hits on a marked enemy that do not crit call
/// down a salvo, and every crit.
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
