namespace FellowshipSim.Core.APL;

/// <summary>
/// One term of a side of a builder row: its arithmetic operator to the term before it (<c>+</c>, <c>-</c>, <c>*</c>,
/// <c>%</c> or <c>%%</c>, null on the first term), its prefix operators (<c>!</c> and <c>-</c>, outermost first, null for
/// none), and a value (<see cref="Path"/>), a number (<see cref="Number"/>) or a parenthesised condition (<see cref="Items"/>).
/// </summary>
public sealed record AplTerm(
    string? Operator,
    string? Prefix = null,
    string? Path = null,
    double? Number = null,
    IReadOnlyList<AplConditionItem>? Items = null);
