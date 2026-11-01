namespace FellowshipSim.UI.Apl;

/// <summary>
/// A pick in a <see cref="ValuePicker"/>: a kind's name (<see cref="Kind"/> a kind id, <see cref="Name"/> its slug),
/// <see cref="ConditionBuilder.NumberKind"/>, <see cref="ConditionBuilder.GroupKind"/>, a math function
/// (<see cref="ConditionBuilder.FunctionKind"/>, <see cref="Name"/> its name), or a prefix (<see cref="Prefix"/>).
/// </summary>
public sealed record ValuePick(string Kind, string? Name = null, string? Prefix = null);
