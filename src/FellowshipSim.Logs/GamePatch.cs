namespace FellowshipSim.Logs;

/// <summary>
/// A game patch: its name, the day it went live, and the combat-log format version (FellowshipLogs' report
/// <c>logVersion</c>) its logs have. The report's <c>gameVersion</c> is 1 for every patch, so it tells patches apart by
/// neither.
/// </summary>
public sealed record GamePatch(string Name, DateOnly Released, int LogVersion)
{
    /// <summary>
    /// The patch the engine models: 0.5.0.0 (datamine export 26136736), live on 2026-10-22 (per the user). Its logs are
    /// logVersion 9; every log before it is 8.
    /// </summary>
    public static GamePatch Current { get; } = new("0.5.0.0", new DateOnly(2026, 10, 22), 9);

    /// <summary>
    /// True when a log that started at <paramref name="startUtc"/> is from this patch or later: started on or after the
    /// day it went live, with its log format or a later one (<paramref name="logVersion"/> null when unknown).
    /// </summary>
    public bool Covers(DateTime startUtc, int? logVersion) =>
        DateOnly.FromDateTime(startUtc) >= Released && (logVersion == null || logVersion >= LogVersion);
}
