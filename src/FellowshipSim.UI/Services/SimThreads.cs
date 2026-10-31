namespace FellowshipSim.UI.Services;

/// <summary>
/// The threads the Sim page offers a sim: on a computer its logical processors, a new sim taking half of them (the rest of
/// the machine keeps the others). The browser app, whose .NET sees one processor, sets both from its sim workers and the
/// browser's cores at start. The Sim page keeps the visitor's choice in the browser's storage, over this default.
/// </summary>
public static class SimThreads
{
    /// <summary>The most threads a sim can run on.</summary>
    public static int Available { get; set; } = Environment.ProcessorCount;

    /// <summary>The threads a new sim runs on: half the logical processors.</summary>
    public static int Default { get; set; } = HalfOf(Environment.ProcessorCount);

    /// <summary>Half of <paramref name="cores"/> logical processors, at least 1.</summary>
    public static int HalfOf(int cores) => Math.Max(1, cores / 2);
}
