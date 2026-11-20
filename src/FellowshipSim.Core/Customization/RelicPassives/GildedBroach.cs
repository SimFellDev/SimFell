namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 782 (Orb of Sanguine Restoration): "When dipping below a health threshold, you are instantly healed for a
/// share of your maximum health. This can occur once per cooldown." The heal is defensive. The passive also grants effect
/// 3274, a Main Stat multiplier (game data: <see cref="Constants.PowerScaler"/>, Strength, Agility and Intellect MultiplyCompound),
/// which the tooltip leaves out (per the user; combat logs: kzGvHPF1n8RqLw2f fight 3, Tariq's Strength is above the gear's
/// and bonuses').
/// </summary>
public partial class GildedBroach : RelicPassive
{
    public const int BuffEffectId = 3274;

    public static readonly AuraDef Buff = AuraDef.Effect(RelicPassiveName, BuffEffectId,
        modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, Constants.PowerScaler));

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => RelicPassiveItems;

    public override void Bind(Hero hero)
    {
        hero.PassiveAuras.Add(Buff);
    }
}
