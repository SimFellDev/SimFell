namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 701: "Your [CORE] abilities have 1% / 2% / 3% / 4% increased Critical Strike chance and 5% / 10% / 15% / 20%
/// increased Critical Strike power." Crit power multiplies the crit multiplier (like Biting Cold). Only the ability's own
/// hits get them, not the hits of an effect it applies. Combat logs: Bursting Ice's pulses (effect 1396) crit for the
/// same multiple of a normal pulse with the glyph as without it (crit over normal, log against engine: Slick at rank 4
/// 0.830 = 1/1.20, Loreuxe at rank 1 0.951 = 1/1.05; 4Ln6PQZCRzc7Wpta fights 77 and 78, jHDC613ZzbTYR8wW fight 18), and
/// crit less often than the engine's chance with the glyph's (Slick 56.4% against 60.7%, Loreuxe 45.2% against 47.3%);
/// their Freezing Torrent ticks have it (crit over normal 1.013, 0.997).
/// </summary>
public class TheSinister : Glyph
{
    public const int GlyphId = 701;
    public const string GlyphName = "The Sinister";
    public static readonly double[] CritChanceByRank = [0.01, 0.02, 0.03, 0.04];
    public static readonly double[] CritPowerByRank = [0.05, 0.10, 0.15, 0.20];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        foreach (var ability in hero.Abilities.Where(a => a.Category == AbilityCategory.Core && a.DamageLogId == a.Id))
        {
            ability.CritChanceBonus += ForRank(CritChanceByRank);
            ability.CritPowerMultiplier *= 1.0 + ForRank(CritPowerByRank);
        }
    }
}
