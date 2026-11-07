namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 62: "Your Armor is increased."</summary>
public class TheOak : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 62;
    public const string ProtectiveEnchantName = "The Oak";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
