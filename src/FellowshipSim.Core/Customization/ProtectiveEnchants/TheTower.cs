namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 59: "Your defensive ability's duration is increased by 4 seconds (Rime: Frost Ward, 4s to 8s in PCaMVTFbZzNLrfx3 fight 5)."</summary>
public class TheTower : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 59;
    public const string ProtectiveEnchantName = "The Tower";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
