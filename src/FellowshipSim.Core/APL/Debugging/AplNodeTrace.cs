namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// One expression of a condition and its value at the decision: the text, the value (1 or 0 for a truth), and the values
/// of its operands. <see cref="Truth"/> is true for a value read as true or false: a comparison, <c>&amp;</c>, <c>|</c> or
/// <c>!</c>. <see cref="Skipped"/> is true for an operand the decision never read, as the right side of <c>&amp;</c>
/// after a false left side.
/// </summary>
public sealed record AplNodeTrace(string Text, double Value, bool Truth, bool Skipped, IReadOnlyList<AplNodeTrace> Operands);
