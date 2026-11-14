using FellowshipSim.Core.Engine;

namespace FellowshipSim.Engine;

/// <summary>
/// A sim's chunks as they finish in other processes (<see cref="SimChunks"/>), their fights merged in chunk order as
/// <see cref="MonteCarloSimulator.Run"/> merges them: the sim's progress and, for a sim to a target error, the chunks of
/// its result. Chunks finish in any order and from any thread.
/// </summary>
public sealed class SimChunkProgress(SimSettings settings)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, DpsMoments> _waiting = [];
    private DpsMoments _merged;
    private int _mergedChunks;
    private int _completed;

    /// <summary>
    /// The chunks of the sim's result: every chunk, or those up to the first at which the fights so far reach the target
    /// error.
    /// </summary>
    public int ResultChunks { get; private set; } = SimChunks.Count(settings);

    /// <summary>Adds chunk <paramref name="chunk"/>'s fights; returns <see cref="ResultChunks"/>.</summary>
    public int Add(int chunk, DpsMoments fights)
    {
        lock (_lock)
        {
            _completed += (int)fights.Count;
            _waiting[chunk] = fights;
            while (_mergedChunks < ResultChunks && _waiting.Remove(_mergedChunks, out var next))
            {
                _merged = _merged.Merge(next);
                _mergedChunks++;
                if (_merged.Reached(settings.TargetError)) ResultChunks = _mergedChunks;
            }
            return ResultChunks;
        }
    }

    /// <summary>The fights done, and for a sim to a target error the margin of error of the fights merged so far.</summary>
    public SimProgress Progress
    {
        get
        {
            lock (_lock) return SimChunks.Progress(settings, _completed, _mergedChunks > 0 ? _merged.MarginPercent : null);
        }
    }
}
