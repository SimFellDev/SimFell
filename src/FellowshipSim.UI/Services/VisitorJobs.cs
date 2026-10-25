using FellowshipSim.Engine.Jobs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The <see cref="SimJobQueue"/> as one <see cref="Visitor"/> sees it, for one circuit or window: their own jobs, where
/// each queued one is in the queue, and cancel and remove for their own jobs alone. Every tab of one browser has the same
/// visitor, so each tab lists the same jobs.
/// </summary>
public sealed class VisitorJobs : IDisposable
{
    private readonly SimJobQueue _queue;

    public VisitorJobs(SimJobQueue queue, Visitor visitor)
    {
        _queue = queue;
        Visitor = visitor;
        _queue.Changed += OnChanged;
        _queue.QueueMoved += OnQueueMoved;
    }

    public Visitor Visitor { get; }

    /// <summary>
    /// One of the visitor's jobs changed, or moved up the queue. It is raised on a job thread or the caller's thread: a
    /// Blazor component handles it through <c>InvokeAsync</c>.
    /// </summary>
    public event Action<SimJob>? Changed;

    /// <summary>The visitor's jobs not removed, oldest first.</summary>
    public IReadOnlyList<SimJob> List() => _queue.List(Visitor.Id);

    /// <summary>The visitor's job <paramref name="id"/>, or null when the visitor has no such job.</summary>
    public SimJob? Get(string id) => _queue.Get(Visitor.Id, id);

    /// <summary>
    /// Queues <paramref name="work"/> as the visitor's job. The work outlives this circuit or window, so it captures
    /// singletons and values only.
    /// </summary>
    public SimJob Enqueue<T>(SimJobKind kind, string title, Func<SimJobContext, Task<T>> work, object? input = null) =>
        _queue.Enqueue(Visitor.Id, kind, title, work, input);

    /// <inheritdoc cref="SimJobQueue.JobsAhead"/>
    public int? JobsAhead(SimJob job) => _queue.JobsAhead(job);

    public bool Cancel(string id) => _queue.Cancel(Visitor.Id, id);

    public bool Remove(string id) => _queue.Remove(Visitor.Id, id);

    private void OnChanged(SimJob job)
    {
        if (job.Owner == Visitor.Id) Changed?.Invoke(job);
    }

    private void OnQueueMoved()
    {
        if (Changed is not { } changed) return;
        foreach (var job in List().Where(j => j.Status == SimJobStatus.Queued)) changed(job);
    }

    public void Dispose()
    {
        _queue.Changed -= OnChanged;
        _queue.QueueMoved -= OnQueueMoved;
    }
}
