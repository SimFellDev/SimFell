namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 782 (Orb of Sanguine Restoration): "When dipping below 20% Health, you are instantly healed for 40% of
/// your maximum health. This can occur once every 180 seconds." The heal is defensive (no effect on damage). The relic
/// also gives +5% Main Stat as a passive (per the user; not in the tooltip). UNVERIFIED: whether it multiplies or adds to the
/// other percentage Main Stat bonuses.
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

    public const double PrimaryStatBonus = 1.05;

    public static readonly AuraDef Passive = AuraDef.Internal("Gilded Broach", 0, 1, StatModifier.More(StatType.PrimaryAttribute, PrimaryStatBonus));

    public override void Bind(Hero hero)
    {
        // A logged Main Stat already includes it.
        if (!hero.Stats.UsesSheetValues) hero.PassiveAuras.Add(hero.Register(Passive));
    }
}
