namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1128: "Instantly Stun target enemy for 4 seconds. Can be used during Global Cooldown." GA_Mara_StunSpender, game
/// data build 26320967: no cost, no cast time, no damage, no Combo Points, and no AbilityType.Skill.Secondary tag, so it
/// keeps Stealth and rolls Hidden Power. The cooldown starts before the stun is tried (commit at ubergraph offset 10, apply at
/// offset 257). <see cref="FinalStratagem"/> ends the cooldown: ReduceCooldownForAllAbilities skips abilities with an ignored
/// tag, and Web Crack has none (exe 0x1452417A0). The ability has no GlobalCooldownGameplayEffectClass (ctor default null, exe
/// 0x1450D7DA7), so it applies no global cooldown and none blocks it (CanActivateAbility exe 0x1450DAB50 -> 0x1450DC780).
/// Combat logs (23 Mara fights, 204 casts): 192 casts under 1s after another cast and 184 under 1s before the next. 2 casts
/// in Stealth keep it. 4 Hidden Power gains fall on a Web Crack with no other cast of Mara's within
/// 60ms (pWmcV42K39GHTLQX fight 23 @ 586.597 and 876.178, T2qLPpmMkrAtJ9B1 fight 18 @ 147.822 and 813.535).
/// </summary>
public class WebCrack : MaraAbility
{
    public const int AbilityId = 1128;
    public const string AbilityName = "Web Crack";

    /// <summary>
    /// Game data ability 1128, row Mara.StunSpender.Cooldown, one charge. GE_Shared_Cooldown_DefaultScale: Cooldown Reduction
    /// shortens it at the cast, Cooldown Recovery sets its rate, and Haste leaves it unchanged. Combat logs (23 Mara fights):
    /// the shortest gaps, 17.066s (v8rhNxVc3PwDqzBA fight 4 @ 440.448) and 17.235s (GXFz3pZ94aQr2chV fight 3 @ 156.206), sit on
    /// 24s x (1 - 0.12 Emerald - 0.10 Ultimatum) / 1.1 Strand of Eternity = 17.02s, the factor of <see cref="ShadowWard.CooldownSeconds"/>.
    /// </summary>
    public const double CooldownSeconds = 24.0;

    public const int StunEffectId = 1583;

    /// <summary>
    /// GA_Mara_StunSpender ubergraph offset 188, literal SetByCaller.Duration on effect 1583, with no Haste. The Base Duration
    /// and combo point rows are unread. Combat logs (23 Mara fights, 202 stuns): a median of 4.011s.
    /// </summary>
    public const double StunSeconds = 4.0;

    public static readonly AuraDef Stun = AuraDef.Effect(AbilityName, StunEffectId, StunSeconds);

    public WebCrack(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        target?.Auras.Apply(Stun, ctx.CurrentTime);
    }
}
