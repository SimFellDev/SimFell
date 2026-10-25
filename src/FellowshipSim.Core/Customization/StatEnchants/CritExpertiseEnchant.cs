namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>Stat enchant 65 (effect 3363): +1 Critical Strike and +1 Expertise rating per rank.</summary>
public class CritExpertiseEnchant : StatEnchant
{
    public const int StatEnchantId = 65;
    public const string StatEnchantName = "Jotunn's Might / Wyvern's Guile / Chimera's Cunning";

    public override int Id => StatEnchantId;
    public override string Name => StatEnchantName;
    protected override (StatType First, StatType Second) Ratings => (StatType.CritRating, StatType.ExpertiseRating);
}
