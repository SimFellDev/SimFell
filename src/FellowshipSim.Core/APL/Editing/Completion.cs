namespace FellowshipSim.Core.APL.Editing;

/// <summary>One item of the completion list: the part's text and its node.</summary>
public sealed record Completion(string Text, CompletionNode Node)
{
    /// <summary>True when the pick has parts after it, so it is written with its '.'.</summary>
    public bool HasMore => Node.Children.Count > 0 && !Node.Leaf;

    /// <summary>The text a pick writes: a pick with parts after it is written with its '.'.</summary>
    public string Written => HasMore ? Text + "." : Text;
}
