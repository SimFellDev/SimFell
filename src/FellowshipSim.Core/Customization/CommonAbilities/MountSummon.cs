namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Summoning a mount. Every mount has its own ability (the 55 abilities whose owner is a mount in the game data), with no
/// cast time or cooldown in the game data. Combat logs: each casts in 1.5s whatever the haste (begincast to cast
/// 1.504-1.517s, 8 mounts in the 15 Rime fights of patch 0.5.0.0). No damage. UNVERIFIED: whether it starts the global
/// cooldown.
/// </summary>
public class MountSummon : Ability
{
    public const string AbilityName = "Mount";
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
    }

    public override bool IsAbility(int abilityId) => Array.IndexOf(MountAbilityIds, abilityId) >= 0;

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
