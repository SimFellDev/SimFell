using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 185 (a relic ability): applies <see cref="Buff"/> to every player within 4000 units, the caster included. Instant,
/// with no global cooldown and no charges, on a cooldown of its own (tag `Cooldown.RelicPower.PartyMoveSpeedBuff`, shared with
/// no other relic). Game data (build 26320967): `ExecuteUbergraph_GA_RelicPower_PartyMoveSpeedBuff` offset 308 applies
/// `GE_RelicPower_PartyMoveSpeedBuff_Buff` alone, and the row `RelicPower.PartyMoveSpeedBuff.CooldownRecoveryIncrease` has no
/// reader. No damage. Combat logs: Mara casts it 0.77s after Skittering Blades (3M2vWr8taxVfTQFY fight 53 @ 89.555) and Stalker
/// Step follows 0.30s after it (fjrCax7VzGmMZw6h fight 26 @ 67.411), and the shortest gap between two casts is 90.1s
/// (b649dhNptyW132Tm fight 5 @ 517.886).
/// </summary>
public class BloodriteFervor : Ability
{
    public const int AbilityId = 185;
    public const string AbilityName = "Bloodrite Fervor";

    /// <summary>Game data: `CT_RelicPowers` row `RelicPower.PartyMoveSpeedBuff.Cooldown`.</summary>
    public const double CooldownSeconds = 90.0;

    public const int BuffEffectId = 156;

    /// <summary>Game data: `CT_RelicPowers` row `RelicPower.PartyMoveSpeedBuff.Duration`.</summary>
    public const double BuffSeconds = 12.0;

    /// <summary>
    /// Effect 156: one modifier, MoveSpeed Multiplicitive x1.5 (`CT_RelicPowers` row `RelicPower.PartyMoveSpeedBuff.MoveSpeedScaler`),
    /// which the engine has no stat for. No stacking type: each application is an instance of its own.
    /// </summary>
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public BloodriteFervor(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        // Game data: no Haste and no Cooldown Recovery reaches a relic cooldown.
        IgnoresCooldownRecovery = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
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
    /// </summary>
    public override void TriggerCooldown(SimulationContext ctx, double cooldownMultiplier = 1.0)
    {
        double gemReduction = Owner.Gems.OfType<Diamond>().FirstOrDefault()?.NodeInSlot(5)?.Value("CooldownReduction") ?? 0.0;
        Owner.CooldownOf(this).Spend(ctx.CurrentTime, Cooldown.TotalSeconds * cooldownMultiplier * (1.0 - gemReduction));
    }
}
