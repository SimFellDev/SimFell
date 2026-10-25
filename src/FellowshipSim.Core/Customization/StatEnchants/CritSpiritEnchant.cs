namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>Stat enchant 67 (effect 3361): +1 Critical Strike and +1 Spirit rating per rank.</summary>
public class CritSpiritEnchant : StatEnchant
{
    public const int StatEnchantId = 67;
    public const string StatEnchantName = "Revenant's Might / Wraith's Guile / Banshee's Cunning";

    public override int Id => StatEnchantId;
    public override string Name => StatEnchantName;
    protected override (StatType First, StatType Second) Ratings => (StatType.CritRating, StatType.SpiritRating);
}
