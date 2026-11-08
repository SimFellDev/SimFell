namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// A relic's passive. Each relic passive class is <c>partial</c>: FellowshipSim.Codex.Generators' <c>RelicPassiveGenerator</c>
/// writes its game ID, name, cooldown, procs per minute, tuning values and relic items from its relic passive record, and
/// the class has the values the game data lacks; <see cref="Bind"/> hooks its effect into the hero.
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
