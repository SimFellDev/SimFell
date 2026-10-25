namespace FellowshipSim.UI.Services;

/// <summary>A fight of a report, and whether its raw fight is in the data folder.</summary>
public sealed record ReportFight(int Id, string Name, bool Kill, double DurationSeconds, bool Downloaded, IReadOnlyList<FightPlayer> Players);
