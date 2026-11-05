namespace FellowshipSim.Core.APL.Editing;

/// <summary>One item of the completion list: the part's text, its node, and the text a pick writes when it differs from the part's.</summary>
public sealed record Completion(string Text, CompletionNode Node, string? Insert = null)
{
    /// <summary>True when the pick has parts after it, so it is written with its '.'.</summary>
    public bool HasMore => Node.Children.Count > 0 && !Node.Leaf;

    /// <summary>The text a pick writes: a pick with parts after it is written with its '.'.</summary>
    public string Written => Insert ?? (HasMore ? Text + "." : Text);

    /// <summary>True when the pick ends where another part starts, so the list opens again after it.</summary>
    public bool OpensNext => HasMore || Insert is [.., '=' or '.'];
}
