namespace FellowshipSim.UI.Services;

/// <summary>
/// The threads the Sim page offers a sim: on a computer its logical processors, a new sim taking all but two (the rest of
/// the machine keeps them). The browser app, whose .NET sees one processor, sets both to its sim workers at start.
/// </summary>
public static class SimThreads
{
    /// <summary>The most threads a sim can run on.</summary>
    public static int Available { get; set; } = Environment.ProcessorCount;

    /// <summary>The threads a new sim runs on.</summary>
    public static int Default { get; set; } = Math.Max(1, Environment.ProcessorCount - 2);
}
