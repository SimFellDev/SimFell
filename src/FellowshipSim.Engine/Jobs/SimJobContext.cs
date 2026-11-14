using System.Globalization;

namespace FellowshipSim.Engine.Jobs;

/// <summary>
/// What a job's work receives: the threads it may use, its cancellation, and its progress. The context is the progress
/// of <see cref="ISimEngine.SimulateAsync"/> (completed fights) and of <see cref="ISimEngine.ReviewAsync"/> (steps).
/// </summary>
public sealed class SimJobContext : IProgress<SimProgress>, IProgress<string>
{
    private readonly SimJobQueue _queue;

    internal SimJobContext(SimJobQueue queue, SimJob job, int degreeOfParallelism)
    {
        _queue = queue;
        Job = job;
        DegreeOfParallelism = degreeOfParallelism;
    }

    public SimJob Job { get; }

    /// <summary>The job's share of the queue's thread budget: pass it as <see cref="SimSettings.DegreeOfParallelism"/>.</summary>
    public int DegreeOfParallelism { get; }

    /// <summary>Cancelled when the job is cancelled or removed.</summary>
    public CancellationToken CancellationToken => Job.Cancellation.Token;

    /// <summary>
    /// Sets the job's progress. A null <paramref name="fraction"/> or <paramref name="message"/> keeps the current one,
    /// and the fraction never falls.
    /// </summary>
    public void Report(double? fraction, string? message = null) => _queue.Report(Job, fraction, message);

    public void Report(SimProgress value) => Report(value.Fraction, value.Text);

    public void Report(string value) => Report(null, value);
}
