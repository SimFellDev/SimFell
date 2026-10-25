namespace FellowshipSim.UI.Services;

/// <summary>A player of a fight, and whether the engine can replay the player's hero.</summary>
public sealed record FightPlayer(int Id, string Name, string Hero, bool Supported);
