namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>Relic passive 780 (Orb of Hidden Riches): "Your Gem Power is increased by 25%." (effect 3271)</summary>
public class CovetousGreed : RelicPassive
{
    public const int RelicPassiveId = 780;
    public const string RelicPassiveName = "Covetous Greed";
    public const int OrbItemId = 5921;
    public const string OrbItemName = "Orb of Hidden Riches";
    public const double GemPowerBonus = 1.25;

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => [new(OrbItemId, OrbItemName)];
    public override double GemPowerMultiplier => GemPowerBonus;

    public override void Bind(Hero hero) { }
}
