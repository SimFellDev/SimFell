namespace FellowshipSim.Core.Engine;

/// <summary>
/// A pending event in the simulation queue. Holding the handle allows the owner to cancel it
/// (e.g. the remaining ticks of a channel that was stopped early).
/// </summary>
public sealed class ScheduledEvent
{
    internal ScheduledEvent(SimTime time, long sequence, Action<SimulationContext>? action, PendingHit? hit, string label)
    {
        Time = time;
        Sequence = sequence;
        Action = action;
        Hit = hit;
        Label = label;
    }

    public SimTime Time { get; }
    internal long Sequence { get; }
    /// <summary>What the event does; null when it is a hit's landing (<see cref="Hit"/>).</summary>
    internal Action<SimulationContext>? Action { get; }
    /// <summary>The travelling hit that lands at <see cref="Time"/>.</summary>
    internal PendingHit? Hit { get; }
    /// <summary>The event's name for debugging. Call sites pass a fixed name, such as the ability's, so scheduling builds no string.</summary>
    public string Label { get; }
    public bool IsCancelled { get; private set; }
    public bool HasFired { get; internal set; }

    public void Cancel() => IsCancelled = true;
}

/// <summary>
/// Time-ordered event queue. Events at the same timestamp fire in the order they were scheduled.
/// </summary>
public class SimulationKernel
{
    private readonly PriorityQueue<ScheduledEvent, (long Ms, long Seq)> _queue = new();
    private long _sequence;

    public ScheduledEvent ScheduleAt(SimTime time, Action<SimulationContext> action, string label = "")
    {
        var ev = new ScheduledEvent(time, _sequence++, action, null, label);
        _queue.Enqueue(ev, (time.Milliseconds, ev.Sequence));
        return ev;
    }

    /// <summary>Schedules <paramref name="hit"/> to land at <paramref name="time"/>.</summary>
    public ScheduledEvent ScheduleAt(SimTime time, PendingHit hit, string label = "")
    {
        var ev = new ScheduledEvent(time, _sequence++, null, hit, label);
        _queue.Enqueue(ev, (time.Milliseconds, ev.Sequence));
        return ev;
    }

    /// <summary>Time of the next live event, or null when the queue is empty.</summary>
    public SimTime? PeekNextTime()
    {
        while (_queue.TryPeek(out var ev, out _))
        {
            if (!ev.IsCancelled) return ev.Time;
            _queue.Dequeue();
        }
        return null;
    }

    /// <summary>Removes and returns the next live event due at or before <paramref name="maxTime"/>.</summary>
    public bool TryDequeueDue(SimTime maxTime, out ScheduledEvent ev)
    {
        while (_queue.TryPeek(out ev!, out _))
        {
            if (ev.IsCancelled)
            {
                _queue.Dequeue();
                continue;
            }
            if (ev.Time > maxTime) return false;
            _queue.Dequeue();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _queue.Clear();
        _sequence = 0;
    }
}
