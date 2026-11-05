namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1205: "Instantly teleport behind target enemy or player and increase your movement speed by 50% for 4 seconds.
/// Stalker Step does not break Stealth. Can be used during Global Cooldown." GA_Mara_ChargedTeleportBehindTarget, game data
/// build 26320967: no cost, no cast time, no global cooldown (GlobalCooldownGameplayEffectClass unset, exe 0x1450E2870 ->
/// 0x1450DC780), and AbilityType.Skill without AbilityType.Skill.Secondary, so it keeps Stealth and rolls Hidden Power.
/// <see cref="FinalStratagem"/> ends the cooldown: Stalker Step has none of the five tags ReduceCooldownForAllAbilities skips,
/// and its 999999 seconds remove every stack of the charge effect, so both charges return (exe 0x1452D62C0).
/// CooldownCommitmentPolicy CCP_Manual: the commit at ubergraph offset 29 raises the commit event (exe 0x1450DC8F0 -> 0x1449DAE00)
/// with no cooldown, so one cast has one Hidden Power roll. The cooldown commit at offset 1487 raises none (exe 0x1449DEE10).
/// That commit and effect 2190 at 1875 run after a successful teleport, and offset 1941 cancels the ability after a failed
/// one. The engine has no positions, so every teleport succeeds, and the teleport and the 1.5 PassiveMoveSpeed of effect 2190
/// change no number.
/// Combat logs (23 Mara fights, 248 casts): 119 casts under 1s after another cast and 211 under 1s before the next. All 30
/// casts in Stealth keep it: the one Stealth removal within 0.1s of a cast is the Skittering Blades 0.1s after it
/// (gd6ByNrf81jc9aWw fight 3 @ 155.090). 8 Hidden Power gains fall on a Stalker Step with no other cast of Mara's within 60ms
/// (mThk4v1f8WGbBd7Q fight 1 @ 1021.108 and 1558.636, T2qLPpmMkrAtJ9B1 fight 18 @ 537.825).
/// </summary>
public class StalkerStep : MaraAbility
{
    public const int AbilityId = 1205;
    public const string AbilityName = "Stalker Step";

    /// <summary>
    /// AC_Hero_Mara row Mara.ChargedTeleportBehindTarget.ChargeDuration. GE_Shared_ChargeCooldownNoHaste: Cooldown Recovery
    /// sets its rate (EffectLogic.TimeRateInfluencedByCooldownRecovery), and Haste leaves it unchanged. CRChargeEffect (exe
    /// 0x1450E71E0): one timer, a spend adds a stack with no restart, and each expiry removes one stack and restarts the timer
    /// at the same duration (0x144A6FC50). The duration is (1 - Cooldown Reduction) x this row, fixed at the first spend from
    /// full charges (0x1450D9990). <see cref="CooldownState.Spend"/> takes its recharge at that spend too. Combat logs (23 Mara
    /// fights): the tightest three casts span 10.687s (GXFz3pZ94aQr2chV fight 3 @ 580.585) and 10.691s (@ 641.600), on 15s x (1 -
    /// 0.12 Emerald - 0.10 Ultimatum) / 1.1 Strand of Eternity = 10.64s, the factor of <see cref="ShadowWard.CooldownSeconds"/>.
    /// </summary>
    public const double CooldownSeconds = 15.0;

    /// <summary>AC_Hero_Mara row Mara.ChargedTeleportBehindTarget.MaxCharges. GetMaximumCharges has no talent test.</summary>
    public const int Charges = 2;

    public const int BuffEffectId = 2190;

    /// <summary>
    /// Row Mara.ChargedTeleportBehindTarget.Talent.IncreasedDamageOrMovementSpeed.MovementSpeedIncrease.Duration on effect 2190,
    /// a scalable float with no Haste. Combat logs: 237 buffs last 4.002s to 4.038s.
    /// </summary>
    public const double BuffSeconds = 4.0;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public StalkerStep(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Mara.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
