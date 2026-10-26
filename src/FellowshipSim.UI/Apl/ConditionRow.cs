using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A row of the condition builder: an <see cref="AplConditionItem"/> the builder changes in place.</summary>
public sealed class ConditionRow
{
    public string? Join { get; set; }
    public bool Not { get; set; }
    /// <summary>The terms of the value, or of the first side of a comparison; null on a group.</summary>
    public List<ConditionTerm>? Left { get; set; }
    /// <summary>Each comparison in turn, with the side it compares to; empty without a comparison.</summary>
    public List<ConditionComparison> Comparisons { get; set; } = [];
    /// <summary>The rows of a group; null on a value row.</summary>
    public List<ConditionRow>? Items { get; set; }
    /// <summary>A group shown closed, as one line of its text. The text has no part for it.</summary>
    public bool Collapsed { get; set; }

    public static List<ConditionRow> From(IReadOnlyList<AplConditionItem> items) =>
        items.Select(i => new ConditionRow
        {
            Join = i.Join,
            Not = i.Not,
            Left = i.Left is { } left ? ConditionTerm.From(left) : null,
            Comparisons = i.Comparisons is { } comparisons ? ConditionComparison.From(comparisons) : [],
            Items = i.Items is { } inner ? From(inner) : null,
        }).ToList();

    public ConditionRow Clone() => new()
    {
        Join = Join,
        Not = Not,
        Left = Left?.Select(t => t.Clone()).ToList(),
        Comparisons = Comparisons.Select(c => c.Clone()).ToList(),
        Items = Items?.Select(i => i.Clone()).ToList(),
        Collapsed = Collapsed,
    };
}
