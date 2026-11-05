namespace FellowshipSim.Core.Engine;

/// <summary>
/// A handle on an event in the simulation queue. Holding the handle allows the owner to cancel it
/// (e.g. the remaining ticks of a channel that was stopped early).
/// </summary>
/// <remarks>
/// Speed: the queue reuses an event once it has fired or been cancelled, so a fight allocates no event per schedule. The
/// handle holds the event's generation: a handle on an event the queue has since reused is not pending, cancels nothing
/// and keeps the time the event had.
/// </remarks>
public readonly struct ScheduledEvent
{
    private readonly SimulationKernel.Node _node;
    private readonly int _generation;
    private readonly SimTime _time;

    internal ScheduledEvent(SimulationKernel.Node node)
    {
        _node = node;
        _generation = node.Generation;
        _time = node.Time;
    }

    private bool Current => _node.Generation == _generation;

    /// <summary>When the event fires.</summary>
    public SimTime Time => Current ? _node.Time : _time;

    /// <summary>The event is queued: it has neither fired nor been cancelled.</summary>
    public bool IsPending => Current && _node is { HasFired: false, IsCancelled: false };

    public void Cancel()
    {
        if (Current) _node.IsCancelled = true;
    }

    internal SimulationKernel.Node? NodeIfCurrent => Current ? _node : null;
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
    // Events fired or cancelled, for reuse.
    private Node? _free;
    // The event dequeued last. It is reused at the next dequeue, once its action has run.
    private Node? _dequeued;

    /// <summary>An event: its time, its action or its hit, and its generation.</summary>
    internal sealed class Node
    {
        public SimTime Time;
        /// <summary>What the event does; null when it is a hit's landing (<see cref="Hit"/>).</summary>
        public Action<SimulationContext>? Action;
        /// <summary>The travelling hit that lands at <see cref="Time"/>.</summary>
        public PendingHit? Hit;
        /// <summary>The event's name for debugging. Call sites pass a fixed name, such as the ability's, so scheduling builds no string.</summary>
        public string Label = "";
        /// <summary>Position in the queue's heap; -1 once out of it.</summary>
        public int HeapIndex = -1;
        /// <summary>The event's reuses: a handle with an earlier generation is on an event gone.</summary>
        public int Generation;
        public bool IsCancelled;
        public bool HasFired;
        public Node? NextFree;
    }

    private readonly struct Entry(long milliseconds, long sequence, Node node)
    {
        public readonly long Milliseconds = milliseconds;
        public readonly long Sequence = sequence;
        public readonly Node Node = node;

        public bool Before(in Entry other) =>
            Milliseconds < other.Milliseconds || (Milliseconds == other.Milliseconds && Sequence < other.Sequence);
    }

    /// <summary>Events in the queue, cancelled ones included until they reach the front.</summary>
    public int Count => _count;

    public ScheduledEvent ScheduleAt(SimTime time, Action<SimulationContext> action, string label = "") =>
        Push(time, action, null, label);

    /// <summary>Schedules <paramref name="hit"/> to land at <paramref name="time"/>.</summary>
    public ScheduledEvent ScheduleAt(SimTime time, PendingHit hit, string label = "") => Push(time, null, hit, label);

    /// <summary>Time of the next live event, or null when the queue is empty.</summary>
    public SimTime? PeekNextTime()
    {
        while (_count > 0)
        {
            var node = _heap[0].Node;
            if (!node.IsCancelled) return node.Time;
            Free(Pop());
        }
        return null;
    }

    /// <summary>Removes the next live event due at or before <paramref name="maxTime"/> and marks it fired.</summary>
    public bool TryDequeueDue(SimTime maxTime, out ScheduledEvent ev)
    {
        if (TryDequeueNode(maxTime) is { } node)
        {
            ev = new ScheduledEvent(node);
            return true;
        }
        ev = default;
        return false;
    }

    /// <summary>
    /// <see cref="TryDequeueDue"/> as the event itself. Its action and hit stay set until the next dequeue or
    /// <see cref="Clear"/>.
    /// </summary>
    internal Node? TryDequeueNode(SimTime maxTime)
    {
        if (_dequeued != null)
        {
            Free(_dequeued);
            _dequeued = null;
        }
        while (_count > 0)
        {
            var node = _heap[0].Node;
            if (node.IsCancelled)
            {
                Free(Pop());
                continue;
            }
            if (node.Time > maxTime) return null;
            Pop();
            node.HasFired = true;
            _dequeued = node;
            return node;
        }
        return null;
    }

    /// <summary>
    /// Moves the queued <paramref name="ev"/> to <paramref name="time"/> with <paramref name="action"/>, in the order a new
    /// event scheduled now would have, and updates the handle. False when it is no longer queued (fired or cancelled).
    /// </summary>
    internal bool Reschedule(ref ScheduledEvent ev, SimTime time, Action<SimulationContext> action)
    {
        if (ev.NodeIfCurrent is not { } node) return false;
        int i = node.HeapIndex;
        if (i < 0 || node.IsCancelled || node.Hit != null) return false;
        node.Time = time;
        node.Action = action;
        var entry = new Entry(time.Milliseconds, _sequence++, node);
        // The sequence only grows, so an unchanged or later time moves the event down and an earlier one moves it up.
        if (i > 0 && entry.Before(_heap[(i - 1) >> 1])) SiftUp(i, entry);
        else SiftDown(i, entry, _count);
        ev = new ScheduledEvent(node);
        return true;
    }

    public void Clear()
    {
        for (int i = 0; i < _count; i++)
        {
            var node = _heap[i].Node;
            node.HeapIndex = -1;
            Free(node);
        }
        Array.Clear(_heap, 0, _count);
        _count = 0;
        _sequence = 0;
        if (_dequeued != null)
        {
            Free(_dequeued);
            _dequeued = null;
        }
    }

    private ScheduledEvent Push(SimTime time, Action<SimulationContext>? action, PendingHit? hit, string label)
    {
        var node = _free ?? new Node();
        _free = node.NextFree;
        node.NextFree = null;
        node.Time = time;
        node.Action = action;
        node.Hit = hit;
        node.Label = label;
        if (_count == _heap.Length) Array.Resize(ref _heap, _heap.Length * 2);
        SiftUp(_count++, new Entry(time.Milliseconds, _sequence++, node));
        return new ScheduledEvent(node);
    }

    // A new generation of the event, holding nothing of the old one.
    private void Free(Node node)
    {
        node.Generation++;
        node.Action = null;
        node.Hit = null;
        node.IsCancelled = false;
        node.HasFired = false;
        node.NextFree = _free;
        _free = node;
    }

    private Node Pop()
    {
        var top = _heap[0].Node;
        top.HeapIndex = -1;
        int last = --_count;
        var entry = _heap[last];
        _heap[last] = default;
        if (last > 0) SiftDown(0, entry, last);
        return top;
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
        entry.Node.HeapIndex = i;
    }
}
