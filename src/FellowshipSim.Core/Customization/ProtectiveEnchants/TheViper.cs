namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 57: "Your defensive ability causes you to heal for a share of all damage you deal for a time."</summary>
public class TheViper : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 57;
    public const string ProtectiveEnchantName = "The Viper";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
