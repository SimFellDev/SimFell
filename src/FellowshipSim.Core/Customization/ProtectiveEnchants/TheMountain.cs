namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 53: "When you take damage that would kill you, your health is set to 1 and you are Immune to damage for 3 seconds (once every 480 seconds)."</summary>
public class TheMountain : ProtectiveEnchant
{
    public const int ProtectiveEnchantId = 53;
    public const string ProtectiveEnchantName = "The Mountain";

    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
