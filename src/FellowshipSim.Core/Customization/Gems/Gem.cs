namespace FellowshipSim.Core.Customization.Gems;

public enum GemColor
{
    Ruby,
    Amethyst,
    Topaz,
    Emerald,
    Sapphire,
    Diamond
}

/// <summary>
/// One node of a gem colour's tree. Nodes 1-5 are the rank I versions of the five slots; nodes 6-10 are rank II and
/// replace the rank I node of the same slot once unlocked. A node unlocks at <see cref="RequiredPower"/> gem power.
/// </summary>
public sealed record GemNode(int Id, int Node, string Name, double RequiredPower, IReadOnlyDictionary<string, double> Values, AuraDef? Grants = null)
{
    /// <summary>The node's slot in its row (1-5), shared by its rank I and rank II versions.</summary>
    public int Slot => (Node - 1) % 5 + 1;
    public bool IsRankTwo => Node > 5;
    public double Value(string key) => Values.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"Gem node {Name} has no value {key}");
}

/// <summary>
/// A gem colour's tree. Each colour lists its nodes (with their game values) at the top of its class, and applies
/// the unlocked ones it models in <see cref="BindNode"/>.
/// </summary>
public abstract class Gem
{
    public abstract GemColor Color { get; }
    public double Power { get; set; }

    /// <summary>The tree of gem colour <paramref name="color"/>.</summary>
    public static Gem Create(GemColor color) => color switch
    {
        GemColor.Emerald => new Emerald(),
        GemColor.Ruby => new Ruby(),
        GemColor.Amethyst => new Amethyst(),
        GemColor.Topaz => new Topaz(),
        GemColor.Sapphire => new Sapphire(),
        _ => new Diamond(),
    };

    /// <summary>Every node of this colour's tree.</summary>
    protected abstract IReadOnlyList<GemNode> NodeTable { get; }

    /// <summary>The nodes unlocked at <see cref="Power"/> (a rank II node replaces the rank I node of its slot).</summary>
    public IReadOnlyList<GemNode> Nodes { get; private set; } = Array.Empty<GemNode>();

    /// <summary>Unlocked nodes this colour does not model yet.</summary>
    public List<string> UnmodeledNodes { get; } = new();

    public void Bind(Hero hero)
    {
        var unlocked = NodeTable.Where(n => Power >= n.RequiredPower).ToList();
        Nodes = unlocked.Where(n => !unlocked.Any(o => o.Node == n.Node + 5)).OrderBy(n => n.Slot).ToList();
        foreach (var node in Nodes)
        {
            if (!BindNode(hero, node)) UnmodeledNodes.Add(node.Name);
        }
    }

    /// <summary>Applies one unlocked node to the hero. Returns false if the node is not modeled.</summary>
    protected abstract bool BindNode(Hero hero, GemNode node);

    public GemNode? NodeInSlot(int slot) => Nodes.FirstOrDefault(n => n.Slot == slot);

    protected static Dictionary<string, double> V(params (string Key, double Value)[] values) => values.ToDictionary(v => v.Key, v => v.Value);
}
