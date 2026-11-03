namespace FellowshipSim.UI.Replay;

/// <summary>A player picked to import from a log: their build, the fight's dungeon with their hero share, or both.</summary>
public sealed record LogImport(PickedPlayer Picked, bool Build, bool Dungeon);
