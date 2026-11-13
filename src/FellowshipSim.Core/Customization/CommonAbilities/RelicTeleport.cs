namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 1506 "Relic Teleport" (a shared ability): the use of a portal of <see cref="ConjurePortal"/>. No cast time, no cooldown
/// and no damage, and combat logs show only a cast event for it. No global cooldown blocks it and it starts none. Game data (build
/// 26320967): `GA_RelicPower_Portal_Interact` sets no `GlobalCooldownGameplayEffectClass`, no `CooldownTags` and no
/// `BlockedByPaddingCooldown`, and the global cooldown check of `UCRGameplayAbility::CanActivateAbility` (exe 0x1450DC780) passes an
/// ability with no such class (null tags at 0x1450DC801). Combat logs: Mara uses it 0.636s and 0.804s after Conjure Portal, inside
/// its 1.5s global cooldown (Zta3LQdvVbPJ9Whz fight 6 @ 187.280, GXFz3pZ94aQr2chV fight 3 @ 463.138), and inside her own 1s global
/// cooldown (v8rhNxVc3PwDqzBA fight 4 @ 678.493, w7khnMKAa4TxXrmC fight 23 @ 704.934).
/// </summary>
public class RelicTeleport : Ability
{
    public const int AbilityId = 1506;
    public const string AbilityName = "Relic Teleport";

    public RelicTeleport(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
