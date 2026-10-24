namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 782 (Orb of Sanguine Restoration): "When dipping below 20% Health, you are instantly healed for 40% of
/// your maximum health. This can occur once every 180 seconds." Defensive: no effect on damage.
/// </summary>
public class GildedBroach : RelicPassive
{
    public const int RelicPassiveId = 782;
    public const string RelicPassiveName = "Gilded Broach";
    public const int OrbItemId = 5919;
    public const string OrbItemName = "Orb of Sanguine Restoration";

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => [new(OrbItemId, OrbItemName)];

    public override void Bind(Hero hero) { }
}
