namespace FellowshipSim.Core.Import;

/// <summary>The gear slots of a build and what each takes.</summary>
public static class GearSlots
{
    public static IReadOnlyList<GearSlot> All { get; } = Enum.GetValues<GearSlot>();

    /// <summary>The kind of item <paramref name="slot"/> takes.</summary>
    public static GearKind Kind(GearSlot slot) => slot switch
    {
        GearSlot.Ring1 or GearSlot.Ring2 => GearKind.Ring,
        GearSlot.Relic1 or GearSlot.Relic2 => GearKind.Relic,
        _ => Enum.Parse<GearKind>(slot.ToString()),
    };

    /// <summary>The slots that take items of <paramref name="kind"/>, in order.</summary>
    public static IEnumerable<GearSlot> Of(GearKind kind) => All.Where(s => Kind(s) == kind);

    /// <summary>The slot's name as a profile line's key ("ring1").</summary>
    public static string Key(GearSlot slot) => slot.ToString().ToLowerInvariant();

    /// <summary>The slot's name for people ("Ring").</summary>
    public static string Label(GearSlot slot) => Kind(slot).ToString();
}
