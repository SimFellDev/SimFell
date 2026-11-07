namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 54: "Your Magic Damage Reduction is increased."</summary>
public class TheShadow : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 54;
    public const string ProtectiveEnchantName = "The Shadow";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
