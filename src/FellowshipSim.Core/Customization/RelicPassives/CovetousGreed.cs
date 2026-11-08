namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>Relic passive 780 (Orb of Hidden Riches): "Your Gem Power is increased." (effect 3271)</summary>
public partial class CovetousGreed : RelicPassive
{
    /// <summary>Tooltip: "Your Gem Power is increased by 25%". The relic passive record has no tuning value.</summary>
    public const double GemPowerBonus = 1.25;

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => RelicPassiveItems;
    public override double GemPowerMultiplier => GemPowerBonus;

    public override void Bind(Hero hero) { }
}
