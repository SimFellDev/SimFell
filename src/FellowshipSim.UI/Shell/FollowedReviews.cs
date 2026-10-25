using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Shell;

/// <summary>Review jobs whose review page opens by itself when the job is done, for one circuit or window.</summary>
public sealed class FollowedReviews
{
    private readonly HashSet<string> _jobIds = [];
    private readonly Lock _lock = new();

    public void Follow(SimJob job)
    {
        lock (_lock) _jobIds.Add(job.Id);
    }

    /// <summary>
    /// True once for a followed job that is done with a review: the job leaves the set, so a second Done notice opens
    /// nothing.
    /// </summary>
    public bool TakeDone(SimJob job, out ReviewSummary review)
    {
        review = null!;
        if (job is not { Status: SimJobStatus.Done, Result: ReviewSummary done }) return false;
        lock (_lock)
        {
            if (!_jobIds.Remove(job.Id)) return false;
        }
        review = done;
        return true;
    }

    /// <summary>Stops following a job that ended without a review.</summary>
    public void Forget(SimJob job)
    {
        if (!job.IsFinished) return;
        lock (_lock) _jobIds.Remove(job.Id);
    }
}
