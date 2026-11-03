using FellowshipSim.UI.Replay;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The players of one hero that a FellowshipLogs link names, the latest first. <see cref="Character"/> is the character's
/// page when the link is one; each player is then that character in one of its recent fights.
/// </summary>
public sealed record LogPlayers(IReadOnlyList<PickedPlayer> Players, FellowshipSim.Logs.CharacterLink? Character);
