namespace FellowshipSim.Codex.Generators;

/// <summary>
/// One tuning key of a record: the key, the key its constant name is from (the key itself, or a rank run's key without
/// <c>Trait.&lt;Row&gt;.</c>), the C# type and literal of its value (an array's elements for a value by rank), whether the
/// value is by rank, and the source its summary names.
/// </summary>
internal sealed record TuningEntry(string Key, string NameKey, string Type, string Literal, bool ByRank, string Source);
