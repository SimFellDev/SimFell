namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1133: reduces the damage you take for a duration and can be used during Global Cooldown.
/// Game data: no cost, no cast time, no global cooldown, no damage, no Spirit roll, and no
/// AbilityType.Skill.Secondary tag, so it keeps Stealth (4 casts in Stealth in the 23 Mara fights of data/fights, each
/// keeping it). The Tower adds to the buff's duration. <see cref="FinalStratagem"/> ends the cooldown:
/// the cooldown reduction skips abilities with an ignored tag, and Shadow Ward has none.
/// </summary>
public class ShadowWard : MaraAbility
{
    public const int AbilityId = 1133;
    public const string AbilityName = "Shadow Ward";

    /// <summary>
    /// Game data ability 1133, cooldown row, one charge. Cooldown
    /// Reduction shortens it at the cast, Cooldown Recovery sets its rate
    /// (EffectLogic.TimeRateInfluencedByCooldownRecovery), and Haste leaves it unchanged. Combat logs (23 Mara fights, 298
    /// casts): none on the engine's cooldown. The tightest gaps sit on the cooldown reduced by Emerald and Ultimatum and
    /// sped by Strand of Eternity (GXFz3pZ94aQr2chV fight 3 @ 645.307 and Prvc4XmbdHBaVRKy fight 12 @ 138.322).
    /// </summary>
    public const double CooldownSeconds = 30.0;

    public const int BuffEffectId = 1592;

    /// <summary>Game data effect 1592. Haste leaves it unchanged.</summary>
    public const double BuffSeconds = 4.0;

    /// <summary>Game data effect 1592.</summary>
    public const double IncomingDamage = 0.6;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, IncomingDamage));

    /// <summary>Seconds added to the buff (The Tower).</summary>
    public double BonusDurationSeconds { get; set; }

    public ShadowWard(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Defensive)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Mara.Auras.Apply(Buff.WithDuration(BuffSeconds + BonusDurationSeconds), ctx.CurrentTime);
    }
}
