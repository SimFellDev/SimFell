using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// A term of a side of a builder row: an <see cref="AplTerm"/> the builder changes in place. A term with no path, number,
/// rows or function has no value picked yet.
/// </summary>
public sealed class ConditionTerm
{
    public string? Operator { get; set; }
    /// <summary>The prefix operators, '!' and '-', outermost first; null for none.</summary>
    public string? Prefix { get; set; }
    public string? Path { get; set; }
    public double? Number { get; set; }
    /// <summary>The rows of the condition in parentheses; null on a value or a number.</summary>
    public List<ConditionRow>? Items { get; set; }
    /// <summary>The name of the math function ("floor"); null on any other term.</summary>
    public string? Function { get; set; }
    /// <summary>The rows of each argument of <see cref="Function"/>; null on any other term.</summary>
    public List<List<ConditionRow>>? Arguments { get; set; }

    public static List<ConditionTerm> From(IReadOnlyList<AplTerm> terms) =>
        terms.Select(t => new ConditionTerm
        {
            Operator = t.Operator,
            Prefix = t.Prefix,
            Path = t.Path,
            Number = t.Number,
            Items = t.Items is { } inner ? ConditionRow.From(inner) : null,
            Function = t.Function,
            Arguments = t.Arguments?.Select(ConditionRow.From).ToList(),
        }).ToList();

    public ConditionTerm Clone() => new()
    {
        Operator = Operator,
        Prefix = Prefix,
        Path = Path,
        Number = Number,
        Items = Items?.Select(r => r.Clone()).ToList(),
        Function = Function,
        Arguments = Arguments?.Select(a => a.Select(r => r.Clone()).ToList()).ToList(),
    };
}
