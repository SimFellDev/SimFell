namespace FellowshipSim.Web;

/// <summary>A dungeon import's answer from a worker: the dungeon's text, or the reason the fight has none.</summary>
public sealed record DungeonReply(string? Text, string? Error);
