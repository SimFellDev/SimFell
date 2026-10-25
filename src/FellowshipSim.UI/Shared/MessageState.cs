namespace FellowshipSim.UI.Shared;

/// <summary>Text and colour of a <see cref="Message"/> that a parent component owns.</summary>
public sealed record MessageState(string Text, MessageKind Kind = MessageKind.Plain)
{
    public static readonly MessageState None = new("");

    public static MessageState Error(string text) => new(text, MessageKind.Error);

    public static MessageState Ok(string text) => new(text, MessageKind.Ok);
}
