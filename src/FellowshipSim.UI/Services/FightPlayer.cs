namespace FellowshipSim.UI.Services;

/// <summary>A player of a fight, whether the engine can replay the player's hero, and whether the player's raw fight is in the data folder.</summary>
public sealed record FightPlayer(int Id, string Name, string Hero, bool Supported, bool Downloaded);
