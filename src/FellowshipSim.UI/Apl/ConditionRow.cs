using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// A row of the condition builder: an <see cref="AplConditionItem"/> the builder changes in place, with the kind picked on
/// a row with no path yet.
/// </summary>
public sealed class ConditionRow
{
    public string? Join { get; set; }
    public bool Not { get; set; }
    public string? Path { get; set; }
    public string? Comparison { get; set; }
    public double? Value { get; set; }
    /// <summary>The rows of a group; null on a value row.</summary>
    public List<ConditionRow>? Items { get; set; }
    /// <summary>The kind picked on a row with no path yet.</summary>
    public string? Kind { get; set; }

    public static List<ConditionRow> From(IReadOnlyList<AplConditionItem> items) =>
        items.Select(i => new ConditionRow
        {
            Join = i.Join,
            Not = i.Not,
            Path = i.Path,
            Comparison = i.Comparison,
            Value = i.Value,
            Items = i.Items is { } inner ? From(inner) : null,
        }).ToList();

    public ConditionRow Clone() => new()
    {
        Join = Join,
        Not = Not,
        Path = Path,
        Comparison = Comparison,
        Value = Value,
        Items = Items?.Select(i => i.Clone()).ToList(),
        Kind = Kind,
    };
}
