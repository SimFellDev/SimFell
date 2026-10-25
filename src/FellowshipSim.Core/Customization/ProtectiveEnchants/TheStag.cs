namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 58: "While your defensive ability is active, you gain movement speed and damage reduction."</summary>
public class TheStag : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 58;
    public const string ProtectiveEnchantName = "The Stag";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
