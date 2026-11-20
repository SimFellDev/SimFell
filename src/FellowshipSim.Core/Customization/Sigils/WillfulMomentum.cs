namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 39: "Your Spirit Rating is increased. Whenever you trigger a Spirit Refund your Main Stat is
/// increased for a time." (effect 2078)
/// </summary>
public partial class WillfulMomentum : Sigil
{
    public const int BuffEffectId = 2078;
    public const double BuffSeconds = 4.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Willful Momentum", BuffEffectId, BuffSeconds);

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        hero.Stats.SpiritRating += ForRank(Constants.AdditionalSpiritRatingByRank);
        var buff = Buff.WithModifiers(StatModifier.Increased(StatType.PrimaryAttributeMultiplier, ForRank(Constants.BuffMainStatAddByRank)));
        hero.OnSpiritRefund += (_, _, ctx) => hero.Auras.Apply(buff, ctx.CurrentTime);
    }
}
