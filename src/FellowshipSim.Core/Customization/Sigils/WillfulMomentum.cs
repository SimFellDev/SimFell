namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 39: "Your Spirit Rating is increased. Whenever you trigger a Spirit Refund your Main Stat is
/// increased for a time." (effect 2078)
/// </summary>
public class WillfulMomentum : Sigil
{
    public const int SigilId = 39;
    public const string SigilName = "Willful Momentum";
    public const int BuffEffectId = 2078;
    public const double BuffSeconds = 4.0;
    public static readonly double[] SpiritRatingByRank = [7, 11, 17, 23];
    public static readonly double[] MainStatByRank = [0.015, 0.026, 0.037, 0.048];

    public static readonly AuraDef Buff = AuraDef.Effect("Willful Momentum", BuffEffectId, BuffSeconds);

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        hero.Stats.SpiritRating += ForRank(SpiritRatingByRank);
        var buff = Buff.WithModifiers(StatModifier.Increased(StatType.PrimaryAttributeMultiplier, 1.0 + ForRank(MainStatByRank)));
        hero.OnSpiritRefund += (_, _, ctx) => hero.Auras.Apply(buff, ctx.CurrentTime);
    }
}
