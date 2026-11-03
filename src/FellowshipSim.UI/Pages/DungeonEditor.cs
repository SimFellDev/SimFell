using System.Globalization;
using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.UI.Pages;

/// <summary>
/// The pull builder's state beside <see cref="SimPageState.Dungeon"/>: the open pulls, by name, and the pull being dragged,
/// by its place. A new pull has the lowest whole number no pull has as its name. A renamed pull keeps each
/// <c>pull_with</c> and <c>chain_with</c> naming it, and a removed pull takes them with it.
/// </summary>
public sealed class DungeonEditor
{
    private readonly HashSet<string> _open = [];

    /// <summary>The names of the open pulls.</summary>
    public IReadOnlySet<string> Open => _open;

    /// <summary>The place of the pull being dragged; null when none is.</summary>
    public int? DragFrom { get; set; }

    public void Toggle(string name)
    {
        if (!_open.Remove(name)) _open.Add(name);
    }

    /// <summary>Closes every pull.</summary>
    public void CloseAll() => _open.Clear();

    /// <summary>The pulls with pull <paramref name="pull"/> added at place <paramref name="index"/> under a new name, open.</summary>
    public IReadOnlyList<DungeonPull> Insert(IReadOnlyList<DungeonPull> pulls, int index, DungeonPull pull)
    {
        string name = Enumerable.Range(1, pulls.Count + 1).Select(n => n.ToString(CultureInfo.InvariantCulture))
            .First(n => pulls.All(p => p.Name != n));
        var order = pulls.ToList();
        order.Insert(index, pull with { Name = name });
        _open.Add(name);
        return order;
    }

    /// <summary>The pulls with the pull at place <paramref name="index"/> removed; the options naming it go.</summary>
    public IReadOnlyList<DungeonPull> Remove(IReadOnlyList<DungeonPull> pulls, int index)
    {
        string name = pulls[index].Name;
        _open.Remove(name);
        return pulls.Where((_, i) => i != index).Select(p => p with
        {
            ChainWith = p.ChainWith == name ? null : p.ChainWith,
            Join = p.Join?.Pull == name ? null : p.Join,
        }).ToList();
    }

    /// <summary>The pulls with the pull at place <paramref name="from"/> moved to place <paramref name="to"/>.</summary>
    public IReadOnlyList<DungeonPull> Move(IReadOnlyList<DungeonPull> pulls, int from, int to)
    {
        var order = pulls.ToList();
        var pull = order[from];
        order.RemoveAt(from);
        order.Insert(to, pull);
        return order;
    }

    /// <summary>
    /// The pulls with the pull at place <paramref name="index"/> named <paramref name="name"/>, and each <c>pull_with</c> and
    /// <c>chain_with</c> naming it naming the new name.
    /// </summary>
    public IReadOnlyList<DungeonPull> Rename(IReadOnlyList<DungeonPull> pulls, int index, string name)
    {
        string old = pulls[index].Name;
        if (_open.Remove(old)) _open.Add(name);
        return pulls.Select((p, i) => p with
        {
            Name = i == index ? name : p.Name,
            ChainWith = p.ChainWith == old ? name : p.ChainWith,
            Join = p.Join is { } j && j.Pull == old ? j with { Pull = name } : p.Join,
        }).ToList();
    }
}
