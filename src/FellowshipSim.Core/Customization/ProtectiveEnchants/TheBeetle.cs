namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>Protective enchant 52: "When you take damage equal to a large share of your max health or more in a single hit, the damage is reduced (on a cooldown)."</summary>
public partial class TheBeetle : ProtectiveEnchant
{
    public override int Id => ProtectiveEnchantId;
    public override string Name => ProtectiveEnchantName;
}
