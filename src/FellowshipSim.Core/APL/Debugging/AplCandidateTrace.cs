namespace FellowshipSim.Core.APL.Debugging;

/// <summary>One enemy <c>target_if</c> read, and the expression's values at it.</summary>
public sealed record AplCandidateTrace(string Enemy, AplNodeTrace Value);
