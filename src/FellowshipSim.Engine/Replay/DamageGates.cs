namespace FellowshipSim.Engine.Replay;

/// <summary>The tests that place a damage ID in its <see cref="FindingGroup"/>, with the constants of <see cref="ReplayFindings"/>.</summary>
/// <param name="InScope">The ID is over <see cref="ReplayFindings.TargetDamageShare"/> of the log's damage.</param>
/// <param name="NormalOff">The mean normal ratio is off 1.</param>
/// <param name="CritOff">The mean crit ratio is off 1.</param>
/// <param name="Scattered">The ratios are wider than the damage spread gives.</param>
/// <param name="EngineOnlyOff">Engine-only hits are over the hit count target.</param>
/// <param name="MissingOff">Missing hits are over the hit count target.</param>
/// <param name="Mispaired">Hits out of range fit other hits of their release, and release totals stay on the log.</param>
public sealed record DamageGates(bool InScope, bool NormalOff, bool CritOff, bool Scattered, bool EngineOnlyOff, bool MissingOff, bool Mispaired);
