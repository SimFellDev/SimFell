namespace FellowshipSim.Logs;

/// <summary>
/// A fight of a character's recent report: the report, its start (Unix milliseconds) and combat-log format version, the
/// fight, and the character's player in it with its hero.
/// </summary>
public sealed record CharacterFight(
    string ReportCode, long ReportStartTime, int? LogVersion, int FightId, string FightName, bool Kill, double DurationSeconds,
    double EndTime, ReportPlayer Player);
