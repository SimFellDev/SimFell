namespace FellowshipSim.Core.APL.Debugging;

/// <summary>An action's <c>target_if</c> at a decision: the value at each active enemy, in order, and the enemy picked (null for none).</summary>
public sealed record AplTargetIfTrace(AplTargetSelection Selection, IReadOnlyList<AplCandidateTrace> Candidates, string? Picked);
