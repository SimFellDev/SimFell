namespace FellowshipSim.Core.Customization.Augmentations;

/// <summary>
/// An item augmentation: an aspect, a sigil or a protective enchant. Each augmentation class lists its game ID, name and
/// per-rank values as constants at the top; <see cref="Bind"/> hooks its effect into the hero.
/// </summary>
public abstract class Augmentation
{
    /// <summary>The augmentation's game ID (combat logs show it + 3,000,000).</summary>
    public abstract int Id { get; }
    public abstract string Name { get; }
    public int Rank { get; init; } = 1;

    /// <summary>This augmentation's value from a per-rank table (rank 1 first).</summary>
    protected double ForRank(IReadOnlyList<double> valuesByRank) => valuesByRank[Math.Clamp(Rank, 1, valuesByRank.Count) - 1];

    public abstract void Bind(Hero hero);
}
