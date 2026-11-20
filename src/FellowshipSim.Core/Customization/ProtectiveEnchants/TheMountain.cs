namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 53: "When you take damage that would kill you, your health is set to the minimum and you are Immune to damage for a time (on a cooldown)."</summary>
public partial class TheMountain : ProtectiveEnchant
{
    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
