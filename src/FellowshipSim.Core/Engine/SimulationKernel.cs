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

    public SimTime Time { get; internal set; }
    internal long Sequence { get; set; }
    /// <summary>Position in the queue's heap; -1 once out of it.</summary>
    internal int HeapIndex = -1;
    /// <summary>What the event does; null when it is a hit's landing (<see cref="Hit"/>).</summary>
    internal Action<SimulationContext>? Action { get; set; }
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
    // Speed: a binary heap of the events with their keys (time, then sequence) beside them, compared as two longs.
    private Entry[] _heap = new Entry[64];
    private int _count;
    private long _sequence;

    private readonly struct Entry(long milliseconds, long sequence, ScheduledEvent ev)
    {
        public readonly long Milliseconds = milliseconds;
        public readonly long Sequence = sequence;
        public readonly ScheduledEvent Event = ev;

        public bool Before(in Entry other) =>
            Milliseconds < other.Milliseconds || (Milliseconds == other.Milliseconds && Sequence < other.Sequence);
    }

    /// <summary>Events in the queue, cancelled ones included until they reach the front.</summary>
    public int Count => _count;

    public ScheduledEvent ScheduleAt(SimTime time, Action<SimulationContext> action, string label = "")
    {
        var ev = new ScheduledEvent(time, _sequence++, action, null, label);
        Push(new Entry(time.Milliseconds, ev.Sequence, ev));
        return ev;
    }

    /// <summary>Schedules <paramref name="hit"/> to land at <paramref name="time"/>.</summary>
    public ScheduledEvent ScheduleAt(SimTime time, PendingHit hit, string label = "")
    {
        var ev = new ScheduledEvent(time, _sequence++, null, hit, label);
        Push(new Entry(time.Milliseconds, ev.Sequence, ev));
        return ev;
    }

    /// <summary>Time of the next live event, or null when the queue is empty.</summary>
    public SimTime? PeekNextTime()
    {
        while (_count > 0)
        {
            var ev = _heap[0].Event;
            if (!ev.IsCancelled) return ev.Time;
            Pop();
        }
        return null;
    }

    /// <summary>Removes and returns the next live event due at or before <paramref name="maxTime"/>.</summary>
    public bool TryDequeueDue(SimTime maxTime, out ScheduledEvent ev)
    {
        while (_count > 0)
        {
            ev = _heap[0].Event;
            if (ev.IsCancelled)
            {
                Pop();
                continue;
            }
            if (ev.Time > maxTime) return false;
            Pop();
            return true;
        }
        ev = null!;
        return false;
    }

    /// <summary>
    /// Moves the queued <paramref name="ev"/> to <paramref name="time"/> with <paramref name="action"/>, in the order a new
    /// event scheduled now would have. False when it is no longer queued (fired or cancelled).
    /// </summary>
    internal bool Reschedule(ScheduledEvent ev, SimTime time, Action<SimulationContext> action)
    {
        int i = ev.HeapIndex;
        if (i < 0 || ev.IsCancelled || ev.Hit != null) return false;
        ev.Time = time;
        ev.Sequence = _sequence++;
        ev.Action = action;
        var entry = new Entry(time.Milliseconds, ev.Sequence, ev);
        // The sequence only grows, so an unchanged or later time moves the event down and an earlier one moves it up.
        if (i > 0 && entry.Before(_heap[(i - 1) >> 1])) SiftUp(i, entry);
        else SiftDown(i, entry, _count);
        return true;
    }

    public void Clear()
    {
        for (int i = 0; i < _count; i++) _heap[i].Event.HeapIndex = -1;
        Array.Clear(_heap, 0, _count);
        _count = 0;
        _sequence = 0;
    }

    private void Push(Entry entry)
    {
        if (_count == _heap.Length) Array.Resize(ref _heap, _heap.Length * 2);
        SiftUp(_count++, entry);
    }

    private void Pop()
    {
        _heap[0].Event.HeapIndex = -1;
        int last = --_count;
        var entry = _heap[last];
        _heap[last] = default;
        if (last == 0) return;
        SiftDown(0, entry, last);
    }

    private void SiftUp(int i, Entry entry)
    {
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (!entry.Before(_heap[parent])) break;
            Place(i, _heap[parent]);
            i = parent;
        }
        Place(i, entry);
    }

    private void SiftDown(int i, Entry entry, int count)
    {
        while (true)
        {
            int child = 2 * i + 1;
            if (child >= count) break;
            if (child + 1 < count && _heap[child + 1].Before(_heap[child])) child++;
            if (!_heap[child].Before(entry)) break;
            Place(i, _heap[child]);
            i = child;
        }
        Place(i, entry);
    }

    private void Place(int i, Entry entry)
    {
        _heap[i] = entry;
        entry.Event.HeapIndex = i;
    }
}
