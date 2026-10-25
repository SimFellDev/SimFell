namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>Stat enchant 69 (effect 3359): +1 Expertise and +1 Spirit rating per rank.</summary>
public class ExpertiseSpiritEnchant : StatEnchant
{
    public const int StatEnchantId = 69;
    public const string StatEnchantName = "Ogre's Might / Vampire's Guile / Sphinx's Cunning";

    public override int Id => StatEnchantId;
    public override string Name => StatEnchantName;
    protected override (StatType First, StatType Second) Ratings => (StatType.ExpertiseRating, StatType.SpiritRating);
}
