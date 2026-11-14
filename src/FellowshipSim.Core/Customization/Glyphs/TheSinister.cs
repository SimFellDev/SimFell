using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 701: "Your [CORE] abilities have increased Critical Strike chance and increased Critical Strike power" by rank
/// (<see cref="Constants.AdditionalCritChanceByRank"/>, <see cref="CritPowerByRank"/>). Crit power multiplies the crit multiplier (like Biting
/// Cold). Only the ability's own hits get them, not the hits of an effect it applies. Combat logs: Bursting Ice's pulses
/// (effect 1396) crit for the same multiple of a normal pulse with the glyph as without it (crit over normal, log against
/// engine: Slick and Loreuxe match the multiple without the glyph's crit power; 4Ln6PQZCRzc7Wpta fights 77 and 78,
/// jHDC613ZzbTYR8wW fight 18), and crit less often than the engine's chance with the glyph's; their Freezing Torrent
/// ticks have it (crit over normal matches the glyph's). UNVERIFIED for Tariq's Heavy Strike and Chain
/// Lightning: no downloaded log has the glyph on Tariq.
/// </summary>
public partial class TheSinister : Glyph
{
    /// <summary>
    /// Tooltip: "5% / 10% / 15% / 20% increased Critical Strike power" (the game data's
    /// <see cref="Constants.CritPowerMultiplierByRank"/> has the multipliers, 1.05 for 5%).
    /// </summary>
    public static readonly double[] CritPowerByRank = [0.05, 0.10, 0.15, 0.20];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        // Every Core ability but Bursting Ice, whose pulses the logs show without the glyph.
        foreach (var ability in hero.Abilities.Where(a => a.Category == AbilityCategory.Core && a is not BurstingIce))
        {
            ability.CritChanceBonus += ForRank(Constants.AdditionalCritChanceByRank);
            ability.CritPowerMultiplier *= 1.0 + ForRank(CritPowerByRank);
        }
    }
}
