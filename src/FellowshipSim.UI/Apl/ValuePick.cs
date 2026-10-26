namespace FellowshipSim.UI.Apl;

/// <summary>
/// A pick in a <see cref="ValuePicker"/>: a kind's name (<see cref="Kind"/> a kind id, <see cref="Name"/> its slug),
/// <see cref="ConditionBuilder.NumberKind"/>, <see cref="ConditionBuilder.GroupKind"/>, or a prefix (<see cref="Prefix"/>).
/// </summary>
public sealed record ValuePick(string Kind, string? Name = null, string? Prefix = null);
