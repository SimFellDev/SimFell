using FellowshipSim.Engine;
using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The fight downloaded to the data folder (<see cref="ReplayLibrary"/>, a minute or more the first time; kept for the
/// next review), then replayed by the engine.
/// </summary>
public sealed class DiskFightReviewer(ReplayLibrary library, LogsSettingsStore settings, ISimEngine engine) : IFightReviewer
{
    // Reviews of two players of one fight would download its raw fight to the same file at once.
    private readonly SemaphoreSlim _oneDownloadAtATime = new(1, 1);

    public async Task<ReviewArtifacts> ReviewAsync(string reportCode, int fightId, int playerId, SimJobContext context)
    {
        var ct = context.CancellationToken;
        string rawFight;
        await _oneDownloadAtATime.WaitAsync(ct);
        try
        {
            rawFight = await library.GetRawFightFileAsync(await settings.CreateClientAsync(ct), reportCode, fightId, context, ct);
        }
        finally
        {
            _oneDownloadAtATime.Release();
        }
        context.Report("Replaying the fight");
        await using var fight = File.OpenRead(rawFight);
        return await engine.ReviewAsync(fight, playerId, context, ct);
    }
}
