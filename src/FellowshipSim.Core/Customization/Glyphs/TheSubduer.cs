namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 704: "You deal more damage to Low Health Enemies. Your [MAJOR] abilities reduce the
/// cooldown of one of your [CONTROL] abilities ..." (effect 3185; the Control cooldowns do not affect damage).
/// UNVERIFIED: no log has this glyph; low health is taken as <see cref="LowHealthThreshold"/> or less.
/// </summary>
public class TheSubduer : Glyph
{
    public const int GlyphId = 704;
    public const string GlyphName = "The Subduer";
    public const double LowHealthThreshold = 0.30;
    public static readonly double[] DamageByRank = [0.05, 0.10, 0.15, 0.20];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double multiplier = 1.0 + ForRank(DamageByRank);
        hero.TargetDamageMultipliers.Add((target, _) => target.HealthFraction <= LowHealthThreshold ? multiplier : 1.0);
    }
}
