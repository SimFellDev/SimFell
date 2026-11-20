namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1128: stuns the target enemy and can be used during Global Cooldown.
/// Game data: no cost, no cast time, no damage, no Combo Points, and no AbilityType.Skill.Secondary tag, so it
/// keeps Stealth and rolls Hidden Power. The cooldown starts before the stun is tried.
/// <see cref="FinalStratagem"/> ends the cooldown: the cooldown reduction skips abilities with an ignored
/// tag, and Web Crack has none. The ability has no global cooldown effect, so it applies no global
/// cooldown and none blocks it.
/// Combat logs (23 Mara fights, 204 casts): most casts fall close to another cast. Casts in Stealth keep it. Hidden
/// Power gains fall on a Web Crack with no other cast of Mara's nearby
/// (pWmcV42K39GHTLQX fight 23 @ 586.597 and 876.178, T2qLPpmMkrAtJ9B1 fight 18 @ 147.822 and 813.535).
/// The cooldown has one charge. Cooldown Reduction shortens it at the cast, Cooldown Recovery sets its rate, and Haste
/// leaves it unchanged. Combat logs (23 Mara fights): the shortest gaps (v8rhNxVc3PwDqzBA fight 4 @ 440.448,
/// GXFz3pZ94aQr2chV fight 3 @ 156.206) sit on the cooldown reduced by Emerald and Ultimatum and sped by Strand of Eternity,
/// the factor of <see cref="ShadowWard.AbilityCooldown"/>.
/// </summary>
public partial class WebCrack : MaraAbility
{
    public const int StunEffectId = 1583;

    /// <summary>
    /// Game data sets a literal duration on effect 1583, with no Haste. The Base Duration
    /// and combo point rows are unread. Combat logs (23 Mara fights, 202 stuns): the median stun matches it.
    /// </summary>
    public const double StunSeconds = 4.0;

    public static readonly AuraDef Stun = AuraDef.Effect(AbilityName, StunEffectId, StunSeconds);

    public WebCrack(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        target?.Auras.Apply(Stun, ctx.CurrentTime);
    }
}
