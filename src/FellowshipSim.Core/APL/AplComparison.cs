namespace FellowshipSim.Core.APL;

/// <summary>A comparison on a builder row (<c>&gt;=</c>, <c>&gt;</c>, <c>=</c>, <c>!=</c>, <c>&lt;=</c> or <c>&lt;</c>) and the terms of the side it compares to.</summary>
public sealed record AplComparison(string Comparison, IReadOnlyList<AplTerm> Terms);
