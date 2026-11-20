namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// The <c>target_if</c> of an ability action or a <c>switch_target</c> at a decision: the value at each active enemy, in
/// order, and the enemy picked (null for none).
/// </summary>
public sealed record AplTargetIfTrace(AplTargetSelection Selection, IReadOnlyList<AplCandidateTrace> Candidates, string? Picked);
