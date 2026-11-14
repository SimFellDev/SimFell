using System.Diagnostics;
using FellowshipSim.Engine;

namespace FellowshipSim.Cli;

/// <summary>
/// One stderr line of completed fights, rewritten in place at most once per <see cref="Interval"/>. Reports are written
/// on the reporting thread, so no line is written after <see cref="Clear"/>.
/// </summary>
public sealed class ProgressLine(TextWriter writer) : IProgress<SimProgress>
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private readonly Lock _lock = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last = -Interval;
    private int _width;
    private bool _cleared;

    public void Report(SimProgress value)
    {
        lock (_lock)
        {
            if (_cleared || _clock.Elapsed - _last < Interval) return;
            _last = _clock.Elapsed;
            string fights = value.TargetError is > 0 ? value.Text : $"{value.CompletedFights:N0}/{value.Fights:N0} fights";
            string text = $"{fights} ({value.Fraction:P0})";
            writer.Write('\r' + text.PadRight(_width));
            writer.Flush();
            _width = Math.Max(_width, text.Length);
        }
    }

    /// <summary>Blanks the line and stops further reports.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cleared = true;
            if (_width == 0) return;
            writer.Write('\r' + new string(' ', _width) + '\r');
            writer.Flush();
        }
    }
}
