using FellowshipSim.UI.Services;

namespace FellowshipSim.UI.Replay;

/// <summary>A player picked in a fight of a report; <see cref="CurrentPatch"/> is false for a log from before the patch the engine models.</summary>
public sealed record PickedPlayer(string ReportCode, ReportFight Fight, FightPlayer Player, bool CurrentPatch = true);
