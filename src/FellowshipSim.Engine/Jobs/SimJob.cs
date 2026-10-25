namespace FellowshipSim.Engine.Jobs;

/// <summary>
/// A sim or a review in the <see cref="SimJobQueue"/>, with its progress. The queue changes it from its job threads and
/// raises <see cref="SimJobQueue.Changed"/> after each change.
/// </summary>
public sealed class SimJob
{
    internal SimJob(string owner, SimJobKind kind, string title, object? input, Func<SimJobContext, Task<object?>> work)
    {
        Owner = owner;
        Kind = kind;
        Title = title;
        Input = input;
        Work = work;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    /// <summary>The one owner who lists, reads, cancels and removes the job, such as a web visitor's ID.</summary>
    public string Owner { get; }
    public SimJobKind Kind { get; }
    public string Title { get; }
    /// <summary>The job's subject as the code that enqueued it gave it, such as a review's fight and player.</summary>
    public object? Input { get; }
    public SimJobStatus Status { get; internal set; } = SimJobStatus.Queued;
    /// <summary>Fraction of the work done, from 0 to 1; null while the job has not reported one.</summary>
    public double? Progress { get; internal set; }
    /// <summary>The job's current step, such as "Downloading the fight from FellowshipLogs".</summary>
    public string? Message { get; internal set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; internal set; }
    public DateTime? FinishedAt { get; internal set; }
    /// <summary>Why the job failed.</summary>
    public string? Error { get; internal set; }
    /// <summary>The work's result once the job is done, such as a <see cref="SimResult"/>.</summary>
    public object? Result { get; internal set; }

    public bool IsFinished => Status is SimJobStatus.Done or SimJobStatus.Failed or SimJobStatus.Cancelled;

    internal Func<SimJobContext, Task<object?>> Work { get; }
    internal CancellationTokenSource Cancellation { get; } = new();
    /// <summary><see cref="System.Diagnostics.Stopwatch"/> timestamp of the last <see cref="SimJobQueue.Changed"/> for this job.</summary>
    internal long LastRaised;
    /// <summary>1 while a delayed <see cref="SimJobQueue.Changed"/> for this job is waiting.</summary>
    internal int RaisePending;
}
