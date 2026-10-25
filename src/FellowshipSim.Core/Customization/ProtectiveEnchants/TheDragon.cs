namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 61: "Your defensive ability grants you an absorb based on your maximum health."</summary>
public class TheDragon : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 61;
    public const string ProtectiveEnchantName = "The Dragon";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
