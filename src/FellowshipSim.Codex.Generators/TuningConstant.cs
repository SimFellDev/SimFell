namespace FellowshipSim.Codex.Generators;

/// <summary>
/// One constant of a record's <c>Constants</c>: its name, the C# type and literal of its value (an array's elements for a
/// value by rank), whether the value is by rank, its tuning keys (shortest first) and the source of its shortest key.
/// </summary>
internal sealed record TuningConstant(string Name, string Type, string Literal, bool ByRank, string Keys, string Source);
