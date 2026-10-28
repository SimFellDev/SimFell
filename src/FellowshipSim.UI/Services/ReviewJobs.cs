using FellowshipSim.Engine;
using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;
using Microsoft.Extensions.Logging;

namespace FellowshipSim.UI.Services;

/// <summary>
/// Replay reviews as <see cref="SimJobKind.Review"/> jobs on the <see cref="SimJobQueue"/>: each job gets its fight and
/// replays it (<see cref="IFightReviewer"/>), and the review's files are saved in the data folder. A done
/// job's <see cref="SimJob.Result"/> is the <see cref="ReviewSummary"/>, and its <see cref="SimJob.Input"/> the
/// <see cref="ReviewTarget"/>. The review is the visitor's (<see cref="VisitorReviews"/>).
/// </summary>
public sealed class ReviewJobs(SimJobQueue queue, ReplayLibrary library, VisitorReviews reviews, IFightReviewer reviewer,
    ILogger<ReviewJobs> log)
{
    public SimJob Start(Visitor visitor, string reportCode, int fightId, int playerId)
    {
        var target = new ReviewTarget(reportCode, fightId, playerId);
        return queue.Enqueue(visitor.Id, SimJobKind.Review, $"Review of {reportCode} fight {fightId}, player {playerId}", async context =>
        {
            try
            {
                var review = await reviewer.ReviewAsync(reportCode, fightId, playerId, context);
                library.Save(review);
                await reviews.AddAsync(visitor, review.Summary, CancellationToken.None);
                return review.Summary;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Replay review of {Report} fight {Fight} player {Player} failed", reportCode, fightId, playerId);
                throw;
            }
        }, target);
    }
}
