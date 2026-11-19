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
public class BloodriteFervor : RelicPower
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

    public BloodriteFervor(Hero owner) : base(owner, AbilityId, AbilityName, CooldownSeconds)
    {
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }
}
