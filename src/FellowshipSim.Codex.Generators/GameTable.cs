namespace FellowshipSim.Codex.Generators;

/// <summary>
/// A game data table: a <c>public static IReadOnlyDictionary</c> property of a static class in
/// <c>FellowshipSim.Core.Import</c>. It has its property name, its summary, the C# types of its keys and values, whether
/// its string keys ignore case, and its entries in order.
/// </summary>
internal sealed record GameTable(string Name, string Summary, string KeyType, string ValueType, bool IgnoreCase, EquatableArray<TableEntry> Entries);
