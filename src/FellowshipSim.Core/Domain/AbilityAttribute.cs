namespace FellowshipSim.Core.Domain;

/// <summary>
/// The ability record of a <c>partial</c> ability class, by its ID. FellowshipSim.Codex.Generators' <c>AbilityGenerator</c>
/// writes the class's game data half from that record, not from the record of the class's name. A record with
/// <c>replaces</c>, <c>subcastOf</c> or <c>triggeredBy</c> has the class of that record as its base class, and its half
/// has the values that differ from that record's.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AbilityAttribute(int id) : Attribute
{
    /// <summary>The ability record's ID.</summary>
    public int Id { get; } = id;
}
