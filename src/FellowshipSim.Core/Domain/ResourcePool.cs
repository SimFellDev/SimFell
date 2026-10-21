namespace FellowshipSim.Core.Domain;

/// <summary>
/// A resource a hero has (Anima, Winter Orbs, Focus, Spirit...), declared in the hero's file. <see cref="AplName"/> is
/// how action lists refer to it (e.g. "winter_orbs"), plus any <see cref="Aliases"/>.
/// </summary>
public sealed record ResourceDef(string Name, double Max, double Initial = 0)
{
    public string AplName => AplNames.GetOrAdd(Name, n => n.ToLowerInvariant().Replace(' ', '_').Replace("'", ""));
    // Not a field of the record (it would join its equality): action lists read it at every decision.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> AplNames = new();
    public IReadOnlyList<string> Aliases { get; init; } = [];
}

public class ResourceState
{
    public double Max { get; set; }
    public double Current { get; set; }
    public double Initial { get; set; }

    public ResourceState(double max, double initial)
    {
        Max = max;
        Current = initial;
        Initial = initial;
    }

    public void Reset() => Current = Initial;
}

public class ResourcePool
{
    private readonly Dictionary<ResourceDef, ResourceState> _resources = new();

    public void Register(ResourceDef type, double max, double initial)
    {
        _resources[type] = new ResourceState(max, initial);
    }

    public void Add(ResourceDef type, double amount)
    {
        if (_resources.TryGetValue(type, out var res))
        {
            res.Current = Math.Min(res.Max, res.Current + amount);
        }
    }

    public bool Consume(ResourceDef type, double amount)
    {
        if (_resources.TryGetValue(type, out var res) && res.Current >= amount)
        {
            res.Current -= amount;
            return true;
        }
        return false;
    }

    public ResourceState Get(ResourceDef type)
    {
        if (!_resources.TryGetValue(type, out var res))
        {
            res = new ResourceState(100, 0);
            _resources[type] = res;
        }
        return res;
    }

    public void Reset()
    {
        foreach (var res in _resources.Values)
        {
            res.Reset();
        }
    }

    public Dictionary<ResourceDef, double> CreateSnapshot()
    {
        var dict = new Dictionary<ResourceDef, double>();
        foreach (var (k, v) in _resources)
        {
            dict[k] = v.Current;
        }
        return dict;
    }

    public void RestoreSnapshot(Dictionary<ResourceDef, double> snapshot)
    {
        foreach (var (k, v) in snapshot)
        {
            if (_resources.TryGetValue(k, out var state))
            {
                state.Current = v;
            }
            else
            {
                _resources[k] = new ResourceState(100, v);
            }
        }
    }
}

