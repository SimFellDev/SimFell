using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 59: "Your defensive ability's duration is increased" (Rime: Frost Ward, longer in PCaMVTFbZzNLrfx3 fight 5).</summary>
public class TheTower : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 59;
    public const string ProtectiveEnchantName = "The Tower";

    /// <summary>
    /// Game data (ability 2077): the duration of effect 1592 increases after it
    /// is applied, with no change to Shadow Ward's cooldown or its damage taken. The log's applybuff
    /// therefore has the unenchanted duration. Combat logs (23 Mara fights): the 6 fights with the enchant have Shadow Ward up
    /// longer after each cast than the 17 without it.
    /// </summary>
    public const double ShadowWardBonusSeconds = 4.0;

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;

    public override void Bind(Hero hero)
    {
        if (hero is Mara mara && mara.GetAbility<ShadowWard>() is { } shadowWard) shadowWard.BonusDurationSeconds += ShadowWardBonusSeconds;
    }
}
