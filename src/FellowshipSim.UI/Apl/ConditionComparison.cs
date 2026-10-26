using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A comparison on a builder row and the terms of the side it compares to: an <see cref="AplComparison"/> the builder changes in place.</summary>
public sealed class ConditionComparison
{
    public string Comparison { get; set; } = "";
    public List<ConditionTerm> Terms { get; set; } = [];

    public static List<ConditionComparison> From(IReadOnlyList<AplComparison> comparisons) =>
        comparisons.Select(c => new ConditionComparison { Comparison = c.Comparison, Terms = ConditionTerm.From(c.Terms) }).ToList();

    public ConditionComparison Clone() => new() { Comparison = Comparison, Terms = Terms.Select(t => t.Clone()).ToList() };
}
