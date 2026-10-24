namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>Stat enchant 66 (effect 3362): +1 Critical Strike and +1 Haste rating per rank.</summary>
public class CritHasteEnchant : StatEnchant
{
    public const int StatEnchantId = 66;
    public const string StatEnchantName = "Griffin's Might / Strix's Guile / Salamander's Cunning";

    public override int Id => StatEnchantId;
    public override string Name => StatEnchantName;
    protected override (StatType First, StatType Second) Ratings => (StatType.CritRating, StatType.HasteRating);
}
