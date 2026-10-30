using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.UI.Pages;

/// <summary>
/// The pull builder's state beside <see cref="SimPageState.Dungeon"/>: the open pulls, by number, and the pull being dragged,
/// by its place. Its changes to the pulls keep them numbered 1 to N in their order, and move each <c>pull_with</c> and
/// <c>chain_with</c> with the pull it names.
/// </summary>
public sealed class DungeonEditor
{
    private readonly HashSet<int> _open = [];

    /// <summary>The numbers of the open pulls.</summary>
    public IReadOnlySet<int> Open => _open;

    /// <summary>The place of the pull being dragged; null when none is.</summary>
    public int? DragFrom { get; set; }

    public void Toggle(int number)
    {
        if (!_open.Remove(number)) _open.Add(number);
    }

    /// <summary>Closes every pull.</summary>
    public void CloseAll() => _open.Clear();

    /// <summary>The pulls with pull <paramref name="pull"/> added at place <paramref name="index"/>, open.</summary>
    public IReadOnlyList<DungeonPull> Insert(IReadOnlyList<DungeonPull> pulls, int index, DungeonPull pull)
    {
        var order = pulls.ToList();
        order.Insert(index, pull);
        var result = Renumber(order, pull);
        _open.Add(index + 1);
        return result;
    }

    /// <summary>The pulls with the pull at place <paramref name="index"/> removed; the options naming it go.</summary>
    public IReadOnlyList<DungeonPull> Remove(IReadOnlyList<DungeonPull> pulls, int index)
    {
        var order = pulls.ToList();
        order.RemoveAt(index);
        return Renumber(order, null);
    }

    /// <summary>The pulls with the pull at place <paramref name="from"/> moved to place <paramref name="to"/>.</summary>
    public IReadOnlyList<DungeonPull> Move(IReadOnlyList<DungeonPull> pulls, int from, int to)
    {
        var order = pulls.ToList();
        var pull = order[from];
        order.RemoveAt(from);
        order.Insert(to, pull);
        return Renumber(order, null);
    }

    /// <summary>
    /// The pulls numbered 1 to N in <paramref name="order"/>, each <c>pull_with</c> and <c>chain_with</c> naming its pull's
    /// new number. <paramref name="added"/> has no number yet, and an option naming a pull no longer there goes. The open
    /// pulls keep their new numbers.
    /// </summary>
    private IReadOnlyList<DungeonPull> Renumber(List<DungeonPull> order, DungeonPull? added)
    {
        var numbers = new Dictionary<int, int>();
        for (int i = 0; i < order.Count; i++)
        {
            if (!ReferenceEquals(order[i], added)) numbers[order[i].Number] = i + 1;
        }
        var open = _open.Where(numbers.ContainsKey).Select(n => numbers[n]).ToList();
        _open.Clear();
        _open.UnionWith(open);
        return order.Select((p, i) => p with
        {
            Number = i + 1,
            ChainWith = p.ChainWith is { } c && numbers.TryGetValue(c, out int chain) ? chain : null,
            Join = p.Join is { } j && numbers.TryGetValue(j.Pull, out int with) ? j with { Pull = with } : null,
        }).ToList();
    }
}
