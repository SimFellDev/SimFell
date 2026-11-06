namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 56: "You take reduced Area of Effect damage."</summary>
public class TheLake : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 56;
    public const string ProtectiveEnchantName = "The Lake";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
