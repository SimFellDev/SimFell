namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 57: "Your defensive ability causes you to heal for 30% of all damage you deal for 8 seconds."</summary>
public class TheViper : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 57;
    public const string ProtectiveEnchantName = "The Viper";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
