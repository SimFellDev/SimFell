namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// A glyph (gear rune). Each glyph class is <c>partial</c>: FellowshipSim.Codex.Generators' <c>GlyphGenerator</c> writes
/// its game ID, name and tuning values (per-rank values as <c>&lt;Name&gt;ByRank</c> arrays) from its glyph record, and the
/// class has the values the game data lacks; <see cref="Bind"/> hooks its effect into the hero.
/// </summary>
public abstract class Glyph
{
    /// <summary>The glyph's game ID. Combat logs give glyphs per-item IDs, so logs are matched by <see cref="Name"/>.</summary>
    public abstract int Id { get; }
    public abstract string Name { get; }
    public int Rank { get; init; } = 1;

    /// <summary>This glyph's value from a per-rank table (rank 1 first).</summary>
    protected double ForRank(IReadOnlyList<double> valuesByRank) => valuesByRank[Math.Clamp(Rank, 1, valuesByRank.Count) - 1];

    public abstract void Bind(Hero hero);
}
