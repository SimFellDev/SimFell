using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;
using FellowshipSim.UI.Shell;

namespace FellowshipSim.Tests.Shell;

/// <summary>A review started from the Import page opens once when its job is done, and never after it fails.</summary>
public class FollowedReviewsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly ReviewSummary Review = new("abc_fight1_p2", "abc", 1, "Fight", null, 2, "Player", "Rime",
        1, 1, 1, 1, 1, 1, 0, DateTime.UtcNow);

    [Fact]
    public async Task A_Done_Review_Opens_Once()
    {
        var queue = new SimJobQueue();
        var followed = new FollowedReviews();
        var job = queue.Enqueue("a", SimJobKind.Review, "review", _ => Task.FromResult(Review));
        followed.Follow(job);
        await Finished(queue, job);

        Assert.True(followed.TakeDone(job, out var review));
        Assert.Equal(Review, review);
        Assert.False(followed.TakeDone(job, out _));
    }

    [Fact]
    public void A_Running_Review_Does_Not_Open()
    {
        var queue = new SimJobQueue();
        var followed = new FollowedReviews();
        var gate = new TaskCompletionSource<ReviewSummary>();
        var job = queue.Enqueue("a", SimJobKind.Review, "review", _ => gate.Task);
        followed.Follow(job);

        Assert.False(followed.TakeDone(job, out _));
        gate.SetResult(Review);
    }

    [Fact]
    public async Task A_Failed_Review_Is_Forgotten()
    {
        var queue = new SimJobQueue();
        var followed = new FollowedReviews();
        var job = queue.Enqueue<ReviewSummary>("a", SimJobKind.Review, "review", _ => throw new InvalidOperationException("no"));
        followed.Follow(job);
        await Finished(queue, job);

        Assert.Equal(SimJobStatus.Failed, job.Status);
        Assert.False(followed.TakeDone(job, out _));
        followed.Forget(job);
        Assert.False(followed.TakeDone(job, out _));
    }

    [Fact]
    public async Task An_Unfollowed_Review_Does_Not_Open()
    {
        var queue = new SimJobQueue();
        var job = queue.Enqueue("a", SimJobKind.Review, "review", _ => Task.FromResult(Review));
        await Finished(queue, job);

        Assert.False(new FollowedReviews().TakeDone(job, out _));
    }

    private static async Task Finished(SimJobQueue queue, SimJob job)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check(SimJob _)
        {
            if (job.IsFinished) done.TrySetResult();
        }
        queue.Changed += Check;
        try
        {
            Check(job);
            await done.Task.WaitAsync(Timeout);
        }
        finally
        {
            queue.Changed -= Check;
        }
    }
}
