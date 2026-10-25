namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>Stat enchant 68 (effect 3360): +1 Expertise and +1 Haste rating per rank.</summary>
public class ExpertiseHasteEnchant : StatEnchant
{
    public const int StatEnchantId = 68;
    public const string StatEnchantName = "Behemoth's Might / Huldra's Guile / Undine's Cunning";

    public override int Id => StatEnchantId;
    public override string Name => StatEnchantName;
    protected override (StatType First, StatType Second) Ratings => (StatType.ExpertiseRating, StatType.HasteRating);
}
