using FellowshipSim.UI.Services;

namespace FellowshipSim.UI.Jobs;

/// <summary>A saved sim in the Sim results sidebar's recent list.</summary>
public sealed record RecentResult(string Key, string Kind, string Title, string Value, DateTime CreatedAt, SavedSimSummary Sim)
{
    public static RecentResult Of(SavedSimSummary sim) =>
        new($"sim:{sim.Id}", "Sim", sim.Title, $"{sim.MeanDps:N0} DPS", sim.CreatedAt, sim);
}
