namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 421: each Spirit Refund you trigger grants Drenched in Blood, which increases your Expertise (effect
/// 2262). Combat logs (Fc8v6yWxD14GQMnV fight 19, 82 buffs): it comes with Rising Spirit.
/// </summary>
public partial class DrenchedInBlood : Legendary
{
    public const int BuffEffectId = 2262;

    public static readonly AuraDef Buff = AuraDef.Effect("Drenched in Blood", BuffEffectId, Constants.BuffDuration,
        modifiers: StatModifier.Add(StatType.Expertise, Constants.BuffAdditionalExpertise));

    public override void Bind(Hero hero) => hero.OnSpiritRefund += (_, _, ctx) => hero.Auras.Apply(Buff, ctx.CurrentTime);
}
