namespace FellowshipSim.UI.Services;

/// <summary>A fight of a report and its players.</summary>
public sealed record ReportFight(int Id, string Name, bool Kill, double DurationSeconds, IReadOnlyList<FightPlayer> Players);
