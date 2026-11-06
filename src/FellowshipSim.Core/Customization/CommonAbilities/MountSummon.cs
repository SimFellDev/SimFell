namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Summoning a mount. Every mount has its own ability (the abilities whose owner is a mount in the game data), with no
/// cooldown in the game data. It casts in <see cref="CastTimeSeconds"/> whatever the haste. No damage.
/// It waits for no global cooldown: the game data gives the mount abilities no global cooldown effect. Combat logs:
/// 3M2vWr8taxVfTQFY fight 53 Arachnid Assault 109.202, Mount 109.634; dBgbn8wD1RxL7Kma fight 9 Arachnid Assault 144.957,
/// Mount 145.358. UNVERIFIED: that it applies no global cooldown: the
/// native default for mounting abilities is unread, and its cast covers a global cooldown in the logs.
/// The mount ability's `CancelAbilitiesWithTag` is `AbilityPrio.Low` and its `BlockAbilitiesWithTag` is `AbilityType.Mounting`:
/// a running mount cast blocks another.
/// </summary>
public class MountSummon : Ability
{
    public const string AbilityName = "Mount";

    /// <summary>Game data: the mount cast duration.</summary>
    public const double CastTimeSeconds = 1.5;

    /// <summary>The game's mount abilities (game data: abilities with a mount as owner).</summary>
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
