namespace FellowshipSim.UI.Services;

/// <summary>
/// What a list of saved sims shows of one: kept beside it (<see cref="SimResultLibrary"/>), so the list never reads a sim's
/// result. Its <see cref="Title"/> and <see cref="Favorite"/> are the sim's own: renaming or favoriting writes this alone.
/// </summary>
public sealed record SavedSimSummary(string Id, string Title, string Hero, double MeanDps, DateTime CreatedAt, bool Favorite = false)
{
    public static SavedSimSummary Of(SavedSim sim) => new(sim.Id, sim.Title, sim.Hero, sim.Result.MeanDps, sim.CreatedAt, sim.Favorite);
}
