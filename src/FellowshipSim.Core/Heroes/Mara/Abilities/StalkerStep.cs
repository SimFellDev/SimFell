namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1205: teleports behind the target enemy or player and increases your movement speed. Stalker Step does not break
/// Stealth and can be used during Global Cooldown.
/// Game data: no cost, no cast time, no global cooldown,
/// and AbilityType.Skill without AbilityType.Skill.Secondary, so it keeps Stealth and rolls Hidden Power.
/// <see cref="FinalStratagem"/> ends the cooldown: Stalker Step has none of the five tags the cooldown reduction skips,
/// and its very long cooldown reduction removes every stack of the charge effect, so both charges return.
/// The cooldown commitment is manual: the first commit raises the commit event
/// with no cooldown, so one cast has one Hidden Power roll. The cooldown commit raises none.
/// That commit and effect 2190 run after a successful teleport, and the ability cancels after a failed
/// one. The engine has no positions, so every teleport succeeds, and the teleport and the passive move speed of effect 2190
/// change no number.
/// Combat logs (23 Mara fights, 248 casts): most casts fall close to another cast. All
/// casts in Stealth keep it: the one Stealth removal shortly after a cast is the Skittering Blades after it
/// (gd6ByNrf81jc9aWw fight 3 @ 155.090). Hidden Power gains fall on a Stalker Step with no other cast of Mara's nearby
/// (mThk4v1f8WGbBd7Q fight 1 @ 1021.108 and 1558.636, T2qLPpmMkrAtJ9B1 fight 18 @ 537.825).
/// <see cref="AbilityCooldown"/> is the charge duration. Cooldown Recovery sets its rate
/// (EffectLogic.TimeRateInfluencedByCooldownRecovery), and Haste leaves it unchanged. The charge effect has
/// one timer, a spend adds a stack with no restart, and each expiry removes one stack and restarts the timer
/// at the same duration. The duration is (1 - Cooldown Reduction) x <see cref="AbilityCooldown"/>, fixed at the first spend
/// from full charges. <see cref="CooldownState.Spend"/> takes its recharge at that spend too. Combat logs (23 Mara
/// fights): the tightest three casts (GXFz3pZ94aQr2chV fight 3 @ 580.585 and @ 641.600) sit on that duration reduced by
/// Emerald and Ultimatum and sped by Strand of Eternity, the factor of <see cref="ShadowWard.AbilityCooldown"/>.
/// The maximum charges have no talent test.
/// </summary>
public partial class StalkerStep : MaraAbility
{
    public const int BuffEffectId = 2190;

    /// <summary>
    /// Game data effect 2190,
    /// a scalable float with no Haste. Combat logs: 237 buffs last this long, within a server frame.
    /// </summary>
    public const double BuffSeconds = 4.0;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public StalkerStep(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Mara.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
