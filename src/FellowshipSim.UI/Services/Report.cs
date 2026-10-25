using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// A FellowshipLogs report with its fights, the fight and player its link names (<see cref="SelectedFightId"/>,
/// <see cref="SelectedPlayerId"/>), and its combat-log format version.
/// </summary>
public sealed record Report(
    string Code, string Title, DateTime StartTime, int? SelectedFightId, int? SelectedPlayerId, IReadOnlyList<ReportFight> Fights,
    int? LogVersion = null)
{
    /// <summary>True when the report is from the patch the engine models (<see cref="GamePatch.Current"/>) or later.</summary>
    public bool CurrentPatch => GamePatch.Current.Covers(StartTime, LogVersion);
}
