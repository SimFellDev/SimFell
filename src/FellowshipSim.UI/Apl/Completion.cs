namespace FellowshipSim.UI.Apl;

/// <summary>One item of the completion list: the part's text and its node.</summary>
public sealed record Completion(string Text, CompletionNode Node)
{
    /// <summary>True when the pick has parts after it, so it is written with its '.'.</summary>
    public bool HasMore => Node.Children.Count > 0 && !Node.Leaf;
}
