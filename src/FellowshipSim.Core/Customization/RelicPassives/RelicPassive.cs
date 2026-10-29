namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// A relic's passive. Each relic passive class lists its game ID, name and values as constants at the top;
/// <see cref="Bind"/> hooks its effect into the hero.
/// </summary>
public abstract class RelicPassive
{
    /// <summary>The relic passive's game ID.</summary>
    public abstract int Id { get; }
    public abstract string Name { get; }

    /// <summary>The relic items that grant it.</summary>
    public abstract IReadOnlyList<RelicItem> Items { get; }

    /// <summary>
    /// Multiplier on every gem colour's power (Covetous Greed), applied to the gear's when the build is imported.
    /// </summary>
    public virtual double GemPowerMultiplier => 1.0;

    public abstract void Bind(Hero hero);
}
