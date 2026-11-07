namespace FellowshipSim.Engine.Replay;

/// <summary>A review's groups of findings, in the order of work of docs/REPLAY_ACCURACY.md.</summary>
public enum FindingGroup
{
    UnmodeledContent,
    Calibration,
    HitCounts,
    ReplayPairing,
    Ticks,
    Resources,
    Auras,
    CastsAndCooldowns,
    Procs,
    WithinTargets,
}
