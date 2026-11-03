namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// One action line a decision read: its line (an index into <see cref="AplFightTrace.Lines"/>), its list depth (0 for
/// <c>actions</c>, one more for each list action entered), its outcome, its condition's values at its target, its
/// <c>target_if</c> candidates, its target, for a <c>variable</c> action the variable's value after it, and for
/// <see cref="AplStepOutcome.CastRefused"/> the engine's reason (<see cref="CastAttempt.Reason"/>).
/// </summary>
public sealed record AplStepTrace(
    int Line,
    int Depth,
    AplStepOutcome Outcome,
    AplNodeTrace? Condition = null,
    AplTargetIfTrace? TargetIf = null,
    string? Target = null,
    double? Value = null,
    string? Refusal = null);
