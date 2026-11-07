namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 421 (Mara's Stalker's Crimson Loop, item 5223; Hungering Blade Hauberk, item 5898): each Spirit Refund
/// you trigger grants Drenched in Blood, which increases your Expertise (effect 2262). Combat logs
/// (Fc8v6yWxD14GQMnV fight 19, 82 buffs): it comes with Rising Spirit.
/// </summary>
public class DrenchedInBlood : Legendary
{
    public const int PowerId = 421;
    public const int RingItemId = 5223;
    public const int ChestItemId = 5898;
    public const int BuffEffectId = 2262;
    public const double BuffSeconds = 8.0;
    public const double ExpertiseBonus = 0.24;

    public static readonly AuraDef Buff = AuraDef.Effect("Drenched in Blood", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.Add(StatType.Expertise, ExpertiseBonus));

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Drenched in Blood";
    public override string HeroName => "Mara";
    public override IReadOnlyList<int> ItemIds => [RingItemId, ChestItemId];

    public override void Bind(Hero hero) => hero.OnSpiritRefund += (_, _, ctx) => hero.Auras.Apply(Buff, ctx.CurrentTime);
}
