namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>Stat enchant 70 (effect 3358): +1 Haste and +1 Spirit rating per rank.</summary>
public class HasteSpiritEnchant : StatEnchant
{
    public const int StatEnchantId = 70;
    public const string StatEnchantName = "Valkyrie's Might / Centaur's Guile / Unicorn's Cunning";

    public override int Id => StatEnchantId;
    public override string Name => StatEnchantName;
    protected override (StatType First, StatType Second) Ratings => (StatType.HasteRating, StatType.SpiritRating);
}
