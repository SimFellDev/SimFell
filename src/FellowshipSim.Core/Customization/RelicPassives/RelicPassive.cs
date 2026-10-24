namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// A relic's passive. Each relic passive class lists its game ID, name and values as constants at the top;
/// <see cref="Bind"/> hooks its effect into the hero.
/// </summary>
public abstract class RelicPassive
{
    /// <summary>Relics a hero can equip at once (per the user: "Heroes can actually equip two relics").</summary>
    public const int MaxPerBuild = 2;

    /// <summary>The relic passive's game ID.</summary>
    public abstract int Id { get; }
    public abstract string Name { get; }

    /// <summary>The relic items that grant it (combat logs list the relic by its item ID).</summary>
    public abstract IReadOnlyList<int> ItemIds { get; }

    /// <summary>
    /// Multiplier on every gem colour's power (Covetous Greed). A character sheet's gem power already includes it, so it
    /// applies only to stats that are not from a sheet.
    /// </summary>
    public virtual double GemPowerMultiplier => 1.0;

    public abstract void Bind(Hero hero);
}
