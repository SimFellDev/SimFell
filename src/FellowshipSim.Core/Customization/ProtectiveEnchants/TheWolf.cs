namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 63: "Your defensive ability heals you and grants damage reduction for a time."</summary>
public class TheWolf : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 63;
    public const string ProtectiveEnchantName = "The Wolf";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
