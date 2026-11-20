namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// A relic item by its game item ID and name (combat logs list both in the gear), and its icon URL from the game data.
/// </summary>
public sealed record RelicItem(int Id, string Name, string? Icon);
