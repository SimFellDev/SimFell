namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Summoning a mount. Every mount has its own ability (the 55 abilities whose owner is a mount in the game data), with no
/// cooldown in the game data. It casts in 1.5s whatever the haste. No damage.
/// It waits for no global cooldown: `GA_Mount_MountUp_Base` and `GA_Mount_Raptor_Armored` have no
/// `GlobalCooldownGameplayEffectClass`, and the check 0x1450DC780 passes on the null class (0x1450DC804). Combat logs:
/// 3M2vWr8taxVfTQFY fight 53 Arachnid Assault 109.202, Mount 109.634; dBgbn8wD1RxL7Kma fight 9 Arachnid Assault 144.957,
/// Mount 145.358. UNVERIFIED: that it applies none (0x1450E2870 applies nothing on the null class, 0x1450E2887): the
/// native default on `CRGameplayAbilityMounting` is unread, and its 1.5s cast covers a global cooldown in the logs.
/// `GA_Mount_MountUp_Base` `CancelAbilitiesWithTag` `AbilityPrio.Low` and `BlockAbilitiesWithTag` `AbilityType.Mounting`:
/// a running mount cast blocks another.
/// </summary>
public class MountSummon : Ability
{
    public const string AbilityName = "Mount";

    /// <summary>`CT_AbilityConstants` row `PlayerAction.Mount.CastingDuration`, `GA_Mount_MountUp_Base` CastingTaskData.</summary>
    public const double CastTimeSeconds = 1.5;

    /// <summary>The game's mount abilities (datamine export 26136736: abilities with a mount as owner).</summary>
    public static readonly int[] MountAbilityIds =
    [
        1683, 1690, 1693, 1694, 1699, 1700, 1702, 1703, 1704, 1705, 1706, 1707, 1708, 1709, 1711, 1712, 1713, 1717, 1720,
        1721, 1727, 1729, 1731, 1737, 1739, 1747, 1756, 1774, 1775, 1777, 1778, 1779, 1780, 1781, 1782, 1787, 1788, 1795,
        1796, 1807, 1808, 1809, 1811, 1812, 1897, 1900, 2046, 2047, 2048, 2049, 2050, 2051, 2052, 2053, 2054,
    ];

    public MountSummon(Hero owner) : base(owner, 0, AbilityName, AbilityCategory.Movement)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        CastTimeScalesWithHaste = false;
        IgnoresGcd = true;
    }

    public override bool IsAbility(int abilityId) => Array.IndexOf(MountAbilityIds, abilityId) >= 0;

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
