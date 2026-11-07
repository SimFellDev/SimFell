
namespace FellowshipSim.Core.APL.Editing;

/// <summary>
/// One '.'-separated part of a path: the parts after it in the order they were added, the token kind its text is coloured
/// by (null for none), its game name, and whether a path ends at it.
/// </summary>
public sealed class CompletionNode(AplTokenKind? kind)
{
    public OrderedDictionary<string, CompletionNode> Children { get; } = new(StringComparer.Ordinal);
    public AplTokenKind? Kind { get; } = kind;
    public string Detail { get; set; } = "";
    public bool Leaf { get; set; }
}
