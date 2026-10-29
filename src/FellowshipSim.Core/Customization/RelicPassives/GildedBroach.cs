namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 782 (Orb of Sanguine Restoration): "When dipping below 20% Health, you are instantly healed for 40% of
/// your maximum health. This can occur once every 180 seconds." The heal is defensive. The passive also grants effect
/// 3274, Main Stat x1.05 (game data: Strength, Agility and Intellect MultiplyCompound 1.05), which the tooltip leaves
/// out (per the user; combat logs: kzGvHPF1n8RqLw2f fight 3, Tariq's Strength is 1.05 times the gear's and bonuses').
/// </summary>
public class GildedBroach : RelicPassive
{
    public const int RelicPassiveId = 782;
    public const string RelicPassiveName = "Gilded Broach";
    public const int OrbItemId = 5919;
    public const string OrbItemName = "Orb of Sanguine Restoration";
    public const int BuffEffectId = 3274;
    public const double PrimaryStatMultiplier = 1.05;

    public static readonly AuraDef Buff = AuraDef.Effect(RelicPassiveName, BuffEffectId,
        modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, PrimaryStatMultiplier));

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => [new(OrbItemId, OrbItemName)];

    public override void Bind(Hero hero)
    {
        hero.PassiveAuras.Add(Buff);
    }
}
