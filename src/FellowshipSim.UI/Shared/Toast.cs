namespace FellowshipSim.UI.Shared;

/// <summary>A toast's text and colour, and its number among its <see cref="ToastProvider"/>'s toasts.</summary>
public sealed record Toast(int Id, MessageState Message);
