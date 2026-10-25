namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 55: "Your defensive ability further reduces Magic Damage taken while active."</summary>
public class TheDune : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 55;
    public const string ProtectiveEnchantName = "The Dune";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
