namespace FellowshipSim.UI.Apl;

/// <summary>
/// The caret of a code field as the browser measured it: its offset in the text, its place in the field (the bottom of
/// the line it is on), the field's width, and whether the field has the focus.
/// </summary>
public sealed record CaretPlace(int At, double Left, double Top, double Width, bool Focused);
