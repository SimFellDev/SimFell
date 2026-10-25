using FellowshipSim.Engine;
using FellowshipSim.Engine.Jobs;

namespace FellowshipSim.Tests;

/// <summary>
/// The sim job queue: jobs run in order up to the concurrency limit, share the thread budget, report throttled progress,
/// and end done, failed or cancelled.
/// </summary>
public class SimJobQueueTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_Job_Ends_Done_With_Its_Result()
    {
        var queue = new SimJobQueue();
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", _ => Task.FromResult(42));
        await Until(queue, job, j => j.IsFinished);
        Assert.Equal(SimJobStatus.Done, job.Status);
        Assert.Equal(42, job.Result);
        Assert.Equal(1, job.Progress);
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task Jobs_Past_The_Limit_Wait_In_Order()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { MaxConcurrentJobs = 2 });
        var gates = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)).ToList();
        var jobs = gates.Select((gate, i) => queue.Enqueue("a", SimJobKind.Sim, $"job {i}", _ => gate.Task)).ToList();

        await Until(queue, jobs[1], j => j.Status == SimJobStatus.Running);
        Assert.Equal(SimJobStatus.Running, jobs[0].Status);
        Assert.Equal(SimJobStatus.Queued, jobs[2].Status);
        Assert.Equal(SimJobStatus.Queued, jobs[3].Status);

        gates[1].SetResult(1);
        await Until(queue, jobs[2], j => j.Status == SimJobStatus.Running);
        Assert.Equal(SimJobStatus.Queued, jobs[3].Status);

        foreach (var gate in gates) gate.TrySetResult(0);
        foreach (var job in jobs) await Until(queue, job, j => j.IsFinished);
        Assert.All(jobs, j => Assert.Equal(SimJobStatus.Done, j.Status));
        Assert.Equal(jobs.Select(j => j.Id), queue.List("a").Select(j => j.Id));
    }

    [Theory]
    [InlineData(8, 2, 4)]
    [InlineData(7, 2, 3)]
    [InlineData(1, 2, 1)]
    public async Task Each_Running_Job_Has_Its_Share_Of_The_Thread_Budget(int budget, int jobs, int threads)
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { ThreadBudget = budget, MaxConcurrentJobs = jobs });
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", c => Task.FromResult(c.DegreeOfParallelism));
        await Until(queue, job, j => j.IsFinished);
        Assert.Equal(threads, job.Result);
    }

    [Fact]
    public async Task A_Queued_Job_Cancels_Without_Running()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { MaxConcurrentJobs = 1 });
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = queue.Enqueue("a", SimJobKind.Sim, "first", _ => gate.Task);
        bool ran = false;
        var second = queue.Enqueue("a", SimJobKind.Sim, "second", _ =>
        {
            ran = true;
            return Task.FromResult(0);
        });

        Assert.True(queue.Cancel("a", second.Id));
        Assert.Equal(SimJobStatus.Cancelled, second.Status);
        gate.SetResult(0);
        await Until(queue, first, j => j.IsFinished);
        Assert.False(ran);
        Assert.Null(second.StartedAt);
        Assert.False(queue.Cancel("a", second.Id));
    }

    [Fact]
    public async Task A_Running_Job_Cancels_When_Its_Work_Stops()
    {
        var queue = new SimJobQueue();
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", async c =>
        {
            await Task.Delay(System.Threading.Timeout.Infinite, c.CancellationToken);
            return 0;
        });
        await Until(queue, job, j => j.Status == SimJobStatus.Running);
        Assert.True(queue.Cancel("a", job.Id));
        await Until(queue, job, j => j.IsFinished);
        Assert.Equal(SimJobStatus.Cancelled, job.Status);
    }

    [Fact]
    public async Task A_Real_Sim_Cancels()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { ThreadBudget = 2 });
        var profile = FellowshipSim.Core.Import.SimcProfile.Parse("rime=\"x\"\nprimary_stat=300\nactions+=/frost_bolt\n");
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", c => new LocalSimEngine().SimulateAsync(profile,
            new SimSettings(1_000_000, 60, DegreeOfParallelism: c.DegreeOfParallelism), c, c.CancellationToken));
        await Until(queue, job, j => j.Progress > 0);
        queue.Cancel("a", job.Id);
        await Until(queue, job, j => j.IsFinished);
        Assert.Equal(SimJobStatus.Cancelled, job.Status);
        Assert.True(job.Progress < 1);
    }

    [Fact]
    public async Task An_Exception_Fails_The_Job_With_Its_Message()
    {
        var queue = new SimJobQueue();
        var refused = queue.Enqueue<int>("a", SimJobKind.Review, "review", _ => throw new SimEngineException("Player 3 did not take part."));
        var broken = queue.Enqueue<int>("a", SimJobKind.Review, "review", _ => throw new InvalidOperationException("broken"));
        // An OperationCanceledException of another token (such as a timeout) is a failure, not a cancellation.
        var timedOut = queue.Enqueue<int>("a", SimJobKind.Review, "review", _ => throw new OperationCanceledException(new CancellationToken(true)));
        foreach (var job in new[] { refused, broken, timedOut }) await Until(queue, job, j => j.IsFinished);

        Assert.Equal(SimJobStatus.Failed, refused.Status);
        Assert.Equal("Player 3 did not take part.", refused.Error);
        Assert.Equal("InvalidOperationException: broken", broken.Error);
        Assert.Equal(SimJobStatus.Failed, timedOut.Status);
    }

    [Fact]
    public async Task Progress_Events_Are_Throttled_And_The_Latest_Arrives()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { ProgressInterval = TimeSpan.FromMilliseconds(200) });
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int events = 0;
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", async c =>
        {
            await start.Task;
            for (int i = 1; i <= 1000; i++) c.Report(i / 1000.0, $"step {i}");
            return await release.Task;
        });
        queue.Changed += j =>
        {
            if (j != job || j.Message == null || j.IsFinished) return;
            Interlocked.Increment(ref events);
            if (j.Message == "step 1000") latest.TrySetResult();
        };

        start.SetResult();
        await latest.Task.WaitAsync(Timeout);
        Assert.InRange(events, 1, 3);
        Assert.Equal(1, job.Progress);
        release.SetResult(0);
        await Until(queue, job, j => j.IsFinished);
    }

    [Fact]
    public async Task A_Failing_Subscriber_Stops_Nothing()
    {
        var queue = new SimJobQueue();
        queue.Changed += _ => throw new InvalidOperationException("subscriber");
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", _ => Task.FromResult(1));
        await Until(queue, job, j => j.IsFinished);
        Assert.Equal(SimJobStatus.Done, job.Status);
    }

    [Fact]
    public async Task A_Removed_Job_Leaves_The_List()
    {
        var queue = new SimJobQueue();
        var job = queue.Enqueue("a", SimJobKind.Sim, "sim", _ => Task.FromResult(1));
        await Until(queue, job, j => j.IsFinished);
        Assert.True(queue.Remove("a", job.Id));
        Assert.Empty(queue.List("a"));
        Assert.Null(queue.Get("a", job.Id));
        Assert.False(queue.Remove("a", job.Id));
    }

    [Fact]
    public async Task An_Owner_Never_Lists_Reads_Cancels_Or_Removes_Another_Owners_Job()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { MaxConcurrentJobs = 1 });
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = queue.Enqueue("a", SimJobKind.Sim, "a's sim", _ => gate.Task);
        var b = queue.Enqueue("b", SimJobKind.Sim, "b's sim", _ => Task.FromResult(2));

        Assert.Equal([a.Id], queue.List("a").Select(j => j.Id));
        Assert.Equal([b.Id], queue.List("b").Select(j => j.Id));
        Assert.Null(queue.Get("b", a.Id));
        Assert.False(queue.Cancel("b", a.Id));
        Assert.False(queue.Remove("b", a.Id));
        Assert.Equal(SimJobStatus.Running, a.Status);

        gate.SetResult(1);
        await Until(queue, b, j => j.IsFinished);
        Assert.Equal(SimJobStatus.Done, a.Status);
        Assert.Equal(SimJobStatus.Done, b.Status);
    }

    [Fact]
    public async Task Jobs_Ahead_Counts_Every_Owners_Queued_Jobs_And_Moves_Up()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { MaxConcurrentJobs = 1 });
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int moved = 0;
        queue.QueueMoved += () => Interlocked.Increment(ref moved);
        var running = queue.Enqueue("a", SimJobKind.Sim, "running", _ => gate.Task);
        var first = queue.Enqueue("b", SimJobKind.Sim, "first", _ => gate.Task);
        var second = queue.Enqueue("a", SimJobKind.Sim, "second", _ => gate.Task);

        Assert.Null(queue.JobsAhead(running));
        Assert.Equal(0, queue.JobsAhead(first));
        Assert.Equal(1, queue.JobsAhead(second));

        int before = moved;
        Assert.True(queue.Cancel("b", first.Id));
        Assert.True(moved > before);
        Assert.Equal(0, queue.JobsAhead(second));

        gate.SetResult(1);
        await Until(queue, second, j => j.IsFinished);
        Assert.Null(queue.JobsAhead(second));
    }

    /// <summary>Waits for <paramref name="condition"/> on the job, checked at each change of the queue.</summary>
    private static async Task Until(SimJobQueue queue, SimJob job, Func<SimJob, bool> condition)
    {
        var met = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check(SimJob _)
        {
            if (condition(job)) met.TrySetResult();
        }
        queue.Changed += Check;
        try
        {
            Check(job);
            await met.Task.WaitAsync(Timeout);
        }
        finally
        {
            queue.Changed -= Check;
        }
    }
}
