namespace FellowshipSim.UI.Build;

/// <summary>
/// Whether a build's enchants can be worn on its gear, and how many more of one kind would still fit (per the user;
/// combat logs, 89 players of patch 0.5.0.0: no item holds more than 2 of a kind, aspects, sigils and glyphs only on
/// head, shoulders, chest, back, hands, waist and feet, one of which the legendary takes). Every piece holds
/// <see cref="EnchantSlots.SlotsPerPiece"/> enchants, at most <see cref="EnchantSlots.MostOfAKindPerPiece"/> of one kind:
/// <list type="bullet">
/// <item>the <see cref="EnchantSlots.ArmorPieces"/> armor pieces take any kind;</item>
/// <item>the <see cref="EnchantSlots.OtherPieces"/> other pieces take stat enchants and gem power bonuses alone, so
/// those fill them first and only the rest goes on armor;</item>
/// <item>a weapon's tree takes 1 aspect, 2 sigils and 2 stat enchants.</item>
/// </list>
/// Protective enchants are apart. A fit is a flow from each kind's count to the pieces that take it.
/// </summary>
public static class GearFit
{
    /// <summary>The kinds that go on gear, in the order the flow numbers them.</summary>
    public static IReadOnlyList<EnchantSlot> Kinds { get; } =
        [EnchantSlot.Aspects, EnchantSlot.Sigils, EnchantSlot.Glyphs, EnchantSlot.StatEnchants, EnchantSlot.Gems];

    /// <summary>The most of <paramref name="kind"/> the armor and other pieces hold, without the weapon's tree.</summary>
    public static int GearLimit(EnchantSlot kind) =>
        EnchantSlots.MostOfAKindPerPiece * (TakesOtherPieces(kind) ? EnchantSlots.ArmorPieces + EnchantSlots.OtherPieces : EnchantSlots.ArmorPieces);

    /// <summary>The most of <paramref name="kind"/> a weapon's tree holds; 0 without a weapon.</summary>
    public static int WeaponLimit(EnchantSlot kind, bool weapon) => !weapon ? 0 : kind switch
    {
        EnchantSlot.Aspects => EnchantSlots.WeaponAspects,
        EnchantSlot.Sigils => EnchantSlots.WeaponSigils,
        EnchantSlot.StatEnchants => EnchantSlots.WeaponStatEnchants,
        _ => 0,
    };

    /// <summary>True when the build's counts can all be worn.</summary>
    public static bool Fits(Func<EnchantSlot, int> used, bool weapon) => Fits(Kinds.Select(used).ToArray(), weapon);

    /// <summary>How many more of <paramref name="kind"/> fit with the rest of the build as it is; 0 when it does not fit.</summary>
    public static int Room(EnchantSlot kind, Func<EnchantSlot, int> used, bool weapon)
    {
        var counts = Kinds.Select(used).ToArray();
        int k = IndexOf(kind);
        int most = GearLimit(kind) + WeaponLimit(kind, weapon) - counts[k];
        int room = 0;
        while (room < most)
        {
            counts[k]++;
            if (!Fits(counts, weapon)) break;
            room++;
        }
        return room;
    }

    private static bool TakesOtherPieces(EnchantSlot kind) => kind is EnchantSlot.StatEnchants or EnchantSlot.Gems;

    private static int IndexOf(EnchantSlot kind)
    {
        for (int i = 0; i < Kinds.Count; i++)
        {
            if (Kinds[i] == kind) return i;
        }
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Protective enchants are not on gear.");
    }

    // Nodes: the source, one per kind, the armor, the other pieces, the weapon's tree, the sink.
    private const int Source = 0, Armor = 6, Other = 7, Tree = 8, Sink = 9, Nodes = 10;

    private static bool Fits(int[] counts, bool weapon)
    {
        var capacity = new int[Nodes, Nodes];
        int per = EnchantSlots.MostOfAKindPerPiece;
        for (int i = 0; i < Kinds.Count; i++)
        {
            var kind = Kinds[i];
            capacity[Source, 1 + i] = counts[i];
            capacity[1 + i, Armor] = per * EnchantSlots.ArmorPieces;
            if (TakesOtherPieces(kind)) capacity[1 + i, Other] = per * EnchantSlots.OtherPieces;
            capacity[1 + i, Tree] = WeaponLimit(kind, weapon);
        }
        capacity[Armor, Sink] = EnchantSlots.SlotsPerPiece * EnchantSlots.ArmorPieces;
        capacity[Other, Sink] = EnchantSlots.SlotsPerPiece * EnchantSlots.OtherPieces;
        capacity[Tree, Sink] = Kinds.Sum(k => WeaponLimit(k, weapon));
        return MaxFlow(capacity) == counts.Sum();
    }

    // Edmonds-Karp on the ten nodes.
    private static int MaxFlow(int[,] capacity)
    {
        int flow = 0;
        var from = new int[Nodes];
        while (true)
        {
            Array.Fill(from, -1);
            from[Source] = Source;
            var queue = new Queue<int>([Source]);
            while (queue.Count > 0 && from[Sink] < 0)
            {
                int node = queue.Dequeue();
                for (int next = 0; next < Nodes; next++)
                {
                    if (from[next] >= 0 || capacity[node, next] <= 0) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            if (from[Sink] < 0) return flow;
            int push = int.MaxValue;
            for (int node = Sink; node != Source; node = from[node]) push = Math.Min(push, capacity[from[node], node]);
            for (int node = Sink; node != Source; node = from[node])
            {
                capacity[from[node], node] -= push;
                capacity[node, from[node]] += push;
            }
            flow += push;
        }
    }
}
