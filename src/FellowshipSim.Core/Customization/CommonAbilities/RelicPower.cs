using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.CommonAbilities;

public abstract class RelicPower : Ability
{
    protected RelicPower(Hero owner, int id, string name, double cooldownSeconds) : base(owner, id, name, AbilityCategory.Relic)
    {
        SetCooldown(cooldownSeconds);
        // Game data: no Haste and no Cooldown Recovery reaches a relic cooldown.
        IgnoresCooldownRecovery = true;
    }

    /// <summary>
    /// The cooldown times the relic cooldown gem factor, at the cast's gem power. Game data (build 26320967):
    /// `GMMC_GemAugmentation.CalculateBaseMagnitude` gives 1 - Blessing of the Artisan's `CooldownReduction` (Diamond slot 5:
    /// offset 810 with the Small effect, row `GemBonus.RelicCooldownReductionBonus.Small.CooldownReductionPercentage`; offset
    /// 1130 with the Medium one, row `...Medium.CooldownReductionPercentage`), else 1. Its query (`CrGetAllActiveEffectsForQuery`,
    /// 0x14521FDC0) runs over every character in the process. The engine reads the hero's own gems alone. `DT_GemDefinitions` row
    /// Diamond: node 5 at `RequiredGemPower` 450 grants `GE_GemBonus_RelicCooldownReductionBonusSmall`, node 10 at 1500
    /// `GE_GemBonus_RelicCooldownReductionBonusMedium`. Neither effect has a `TargetTagRequirementsGameplayEffectComponent`, so both
    /// apply to Mara: their `Character.PrimaryAttribute.Strength` is in the deprecated `ApplicationTagRequirements` field (definition
    /// +0x4B0, read by no routine but the constructor 0x144A63AB0 and the destructor 0x144A65000).
    /// GA_RelicPower_PartyMoveSpeedBuff, GA_RelicPower_Portal and GA_RelicPower_HealthPotion each have CooldownEffectBaseClass
    /// GE_Shared_Cooldown_RelicScale, whose duration is this calculation alone, so no Haste, Cooldown Recovery or ability cooldown
    /// reduction reaches it.
    /// </summary>
    public override void TriggerCooldown(SimulationContext ctx, double cooldownMultiplier = 1.0)
    {
        double gemReduction = Owner.Gems.OfType<Diamond>().FirstOrDefault()?.NodeInSlot(5)?.Value("CooldownReduction") ?? 0.0;
        Owner.CooldownOf(this).Spend(ctx.CurrentTime, Cooldown.TotalSeconds * cooldownMultiplier * (1.0 - gemReduction));
    }
}
