using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// Gets a logged fight and replays one of its players for <see cref="ReviewJobs"/>. <see cref="DiskFightReviewer"/>
/// downloads the fight to the data folder and replays it with the engine; the browser app downloads and replays it on a
/// sim worker, so the fight never sits in the page's memory.
/// </summary>
public interface IFightReviewer
{
    Task<ReviewArtifacts> ReviewAsync(string reportCode, int fightId, int playerId, SimJobContext context);
}
