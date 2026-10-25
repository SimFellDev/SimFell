using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>
/// The job queue as one visitor sees it: every tab of one visitor has the same jobs, another visitor's jobs never show,
/// and a queued job hears when another visitor's job moves the queue.
/// </summary>
public sealed class VisitorJobsTests
{
    private static readonly Visitor A = new("a1b2", OwnsDataFolder: false);
    private static readonly Visitor B = new("c3d4", OwnsDataFolder: false);

    [Fact]
    public void Two_Tabs_Of_One_Visitor_List_The_Same_Jobs_And_Another_Visitor_Lists_None()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { MaxConcurrentJobs = 1 });
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstTab = new VisitorJobs(queue, A);
        using var secondTab = new VisitorJobs(queue, A);
        using var other = new VisitorJobs(queue, B);
        var heard = new List<string>();
        other.Changed += job => heard.Add(job.Id);

        var job = firstTab.Enqueue(SimJobKind.Sim, "sim", _ => gate.Task);

        Assert.Equal([job.Id], secondTab.List().Select(j => j.Id));
        Assert.Same(job, secondTab.Get(job.Id));
        Assert.Empty(other.List());
        Assert.Null(other.Get(job.Id));
        Assert.False(other.Cancel(job.Id));
        Assert.True(secondTab.Cancel(job.Id));
        Assert.Empty(heard);
    }

    [Fact]
    public void A_Queued_Job_Hears_When_Another_Visitors_Job_Leaves_The_Queue()
    {
        var queue = new SimJobQueue(new SimJobQueueOptions { MaxConcurrentJobs = 1 });
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var a = new VisitorJobs(queue, A);
        using var b = new VisitorJobs(queue, B);
        a.Enqueue(SimJobKind.Sim, "running", _ => gate.Task);
        var bQueued = b.Enqueue(SimJobKind.Sim, "b queued", _ => gate.Task);
        var aQueued = a.Enqueue(SimJobKind.Sim, "a queued", _ => gate.Task);
        var heard = new List<SimJob>();
        a.Changed += heard.Add;

        Assert.Equal(1, a.JobsAhead(aQueued));
        b.Cancel(bQueued.Id);

        Assert.Equal([aQueued], heard);
        Assert.Equal(0, a.JobsAhead(aQueued));
        gate.SetResult(1);
    }
}
