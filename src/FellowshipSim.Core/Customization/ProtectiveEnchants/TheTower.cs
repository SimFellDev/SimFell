using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 59: "Your defensive ability's duration is increased by 4 seconds (Rime: Frost Ward, 4s to 8s in PCaMVTFbZzNLrfx3 fight 5)."</summary>
public class TheTower : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 59;
    public const string ProtectiveEnchantName = "The Tower";

    /// <summary>
    /// Game data row Perk.IncreasedDurationOrReducedCooldown.IncreasedDurationInSeconds, read by
    /// GA_Perk_Passive_IncreasedDurationOrReducedCooldown (ability 2077): IncreaseActiveEffectDuration on effect 1592 after it
    /// is applied (ubergraph offset 190), with no change to Shadow Ward's cooldown or its damage taken. The log's applybuff
    /// therefore has duration 4000. Combat logs (23 Mara fights): the 6 fights with the enchant have Shadow Ward up for 8.00s
    /// to 8.04s after each cast, the 17 without it 4.00s to 4.05s.
    /// </summary>
    public const double ShadowWardBonusSeconds = 4.0;

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;

    public override void Bind(Hero hero)
    {
        if (hero is Mara mara && mara.GetAbility<ShadowWard>() is { } shadowWard) shadowWard.BonusDurationSeconds += ShadowWardBonusSeconds;
    }
}
