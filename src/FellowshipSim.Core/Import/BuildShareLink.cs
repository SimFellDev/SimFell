using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Import;

/// <summary>
/// A build as a share link: <c>{site}build#{hero}.{code}.{name}</c>, e.g.
/// <c>https://simfelldev.github.io/build#elarion.YSFs…llqw.Iunno</c>. The hero is its name in lower case, the code holds the
/// rest of the build, and the name is the build's, escaped as URL data (optional: a link without one takes the hero's
/// name). The code keeps game IDs, never positions in lists of the engine's, so a link keeps working as content is added.
/// <para>
/// Code version 1 is bits written as URL-safe base 64, 0 bits to the last character. Numbers are Exp-Golomb (EG, of the
/// order given; a list's count is order 0):
/// <list type="bullet">
/// <item>EG0 the version; 3 bits: values in thousandths, stats from an old profile, max health given.</item>
/// <item>The primary stat, stamina, armor and the crit, haste, expertise and spirit ratings, EG7 (EG17 in thousandths);
/// then max health when given, EG14 (EG24).</item>
/// <item>Lists sorted by game ID, each its count, then each item's ID as its gap from the one before (the first's from 0),
/// then what the item holds: talents; augmentations and glyphs, each with its rank less 1 (EG0); gem power by colour,
/// each with its power (EG9, EG19); socketed gems; gear gem bonuses by colour, each with its count (EG0); stat enchants,
/// each with its rating (EG3) and its count (EG0), 0 for none.</item>
/// <item>Relic 1 and relic 2, then (after its count) each legendary, then the weapon ability: 0 for none, else 1 + the
/// zigzag of its ID less <see cref="RelicBase"/>, <see cref="LegendaryBase"/> or <see cref="WeaponBase"/> (EG1;
/// a legendary is never none, so it has no 1 +).</item>
/// </list>
/// Close IDs cost a few bits each, so most of a build is its stats and gem power.
/// </para>
/// </summary>
public static class BuildShareLink
{
    public const int Version = 1;

    /// <summary>What a link has between the site's address and the build.</summary>
    public const string Marker = "build#";

    // The IDs the usual relics, legendaries and weapons are near (Orb of Hidden Riches, Elarion's leggings, Asha's
    // Chronoshift): any ID is written, these only make the usual ones short.
    public const int RelicBase = 5921;
    public const int LegendaryBase = 5898;
    public const int WeaponBase = 1558;

    // Exp-Golomb orders: each fits the size of what it holds.
    private const int StatOrder = 7, HealthOrder = 14, GemPowerOrder = 9, ThousandthsExtraOrder = 10;
    private const int TalentFirstOrder = 7, TalentGapOrder = 2;
    private const int AugmentationFirstOrder = 3, AugmentationGapOrder = 1;
    private const int GlyphFirstOrder = 8, GlyphGapOrder = 1;
    private const int GemFirstOrder = 4, EnchantFirstOrder = 6, EnchantRatingOrder = 3, OffsetOrder = 1;

    /// <summary>Most items a list in a code can have (a longer one is not a build).</summary>
    private const int MaxListItems = 64;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>The share link of <paramref name="build"/> on the site at <paramref name="siteAddress"/> (ending in '/').</summary>
    public static string Create(string siteAddress, CharacterBuild build)
    {
        string link = $"{siteAddress}{Marker}{build.Hero.ToLowerInvariant()}.{Code(build)}";
        string name = build.Name.Trim();
        if (name.Length == 0 || name.Equals(build.Hero, StringComparison.OrdinalIgnoreCase)) return link;
        string escaped = Uri.EscapeDataString(name);
        // Chat apps leave a final dot out of a link: escape it.
        if (escaped.EndsWith('.')) escaped = escaped[..^1] + "%2E";
        return $"{link}.{escaped}";
    }

    /// <summary>
    /// The build of the share link in <paramref name="text"/> (a whole link, or text holding one), or null when it holds
    /// none. Throws <see cref="FormatException"/> for a link that is not a build.
    /// </summary>
    public static CharacterBuild? TryRead(string text)
    {
        int at = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        string rest = text[(at + Marker.Length)..].Trim();
        int end = rest.IndexOfAny([' ', '\r', '\n', '\t']);
        if (end >= 0) rest = rest[..end];
        var parts = rest.Split('.', 3);
        if (parts.Length < 2) throw new FormatException("The share link has no build after its hero.");
        string hero = BuildHeroes.Find(parts[0])
                      ?? throw new FormatException($"The share link's hero \"{parts[0]}\" is not a hero of the game.");
        var build = FromCode(parts[1]);
        build.Hero = hero;
        build.Name = parts.Length == 3 && parts[2].Length > 0 ? Uri.UnescapeDataString(parts[2]) : hero;
        return build;
    }

    /// <summary>The code of <paramref name="build"/>: everything but its hero and name.</summary>
    public static string Code(CharacterBuild b)
    {
        var w = new BitWriter();
        w.EG(Version, 0);
        double[] stats = Stats(b);
        bool thousandths = stats.Append(b.MaxHealth).Concat(b.GemPower.Values).Any(v => v != Math.Floor(v));
        int extra = thousandths ? ThousandthsExtraOrder : 0;
        w.Bit(thousandths);
        w.Bit(b.StatsFromOldProfile);
        w.Bit(b.MaxHealth > 0);
        foreach (double s in stats) w.EG(Number(s, thousandths), StatOrder + extra);
        if (b.MaxHealth > 0) w.EG(Number(b.MaxHealth, thousandths), HealthOrder + extra);

        WriteSorted(w, b.Talents, id => id, TalentFirstOrder, TalentGapOrder, _ => { });
        WriteSorted(w, b.Augmentations, e => e.Id, AugmentationFirstOrder, AugmentationGapOrder, e => w.EG(Rank(e.Rank), 0));
        WriteSorted(w, b.Glyphs, e => e.Id, GlyphFirstOrder, GlyphGapOrder, e => w.EG(Rank(e.Rank), 0));
        WriteSorted(w, b.GemPower.ToList(), g => (int)g.Key, 0, 0, g => w.EG(Number(g.Value, thousandths), GemPowerOrder + extra));
        WriteSorted(w, b.SocketedGemIds, id => id, GemFirstOrder, 0, _ => { });
        WriteSorted(w, b.GearGemBonuses.ToList(), g => (int)g.Key, 0, 0, g => w.EG(Whole(g.Value), 0));
        // One list of every stat enchant, with its rating and its count.
        var enchants = b.StatEnchants.Select(e => e.Id).Union(b.StatEnchantCounts.Keys).ToList();
        WriteSorted(w, enchants, id => id, EnchantFirstOrder, 0, id =>
        {
            w.EG(Whole(b.StatEnchants.FirstOrDefault(e => e.Id == id)?.Rank ?? 0), EnchantRatingOrder);
            w.EG(Whole(b.StatEnchantCounts.GetValueOrDefault(id)), 0);
        });

        WriteOptional(w, b.Relic1ItemId, RelicBase);
        WriteOptional(w, b.Relic2ItemId, RelicBase);
        w.EG(b.LegendaryItemIds.Count, 0);
        foreach (int id in b.LegendaryItemIds) w.EG(Zigzag(id - LegendaryBase), OffsetOrder);
        WriteOptional(w, b.WeaponAbilityId, WeaponBase);
        return w.Text();
    }

    /// <summary>The build of <paramref name="code"/>, with no hero or name. Throws <see cref="FormatException"/> for a code that is not one.</summary>
    public static CharacterBuild FromCode(string code)
    {
        var r = new BitReader(code);
        long version = r.EG(0);
        if (version != Version) throw new FormatException($"The share link is of version {version}; this SimFell reads version {Version}.");
        bool thousandths = r.Bit(), old = r.Bit(), health = r.Bit();
        int extra = thousandths ? ThousandthsExtraOrder : 0;
        double N(int order) => thousandths ? r.EG(order + extra) / 1000.0 : r.EG(order);
        var b = new CharacterBuild
        {
            StatsFromOldProfile = old,
            PrimaryStat = N(StatOrder), Stamina = N(StatOrder), Armor = N(StatOrder), CritRating = N(StatOrder),
            HasteRating = N(StatOrder), ExpertiseRating = N(StatOrder), SpiritRating = N(StatOrder),
        };
        if (health) b.MaxHealth = N(HealthOrder);

        ReadSorted(r, TalentFirstOrder, TalentGapOrder, id => b.Talents.Add(id));
        ReadSorted(r, AugmentationFirstOrder, AugmentationGapOrder, id => b.Augmentations.Add(new BuildEntry(id, r.Int(0) + 1)));
        ReadSorted(r, GlyphFirstOrder, GlyphGapOrder, id => b.Glyphs.Add(new BuildEntry(id, r.Int(0) + 1)));
        ReadSorted(r, 0, 0, c => b.GemPower[Color(c)] = N(GemPowerOrder));
        ReadSorted(r, GemFirstOrder, 0, id => b.SocketedGemIds.Add(id));
        ReadSorted(r, 0, 0, c => b.GearGemBonuses[Color(c)] = r.Int(0));
        ReadSorted(r, EnchantFirstOrder, 0, id =>
        {
            int rating = r.Int(EnchantRatingOrder), count = r.Int(0);
            if (rating > 0) b.StatEnchants.Add(new BuildEntry(id, rating));
            if (count > 0) b.StatEnchantCounts[id] = count;
        });

        b.Relic1ItemId = ReadOptional(r, RelicBase);
        b.Relic2ItemId = ReadOptional(r, RelicBase);
        long legendaries = r.EG(0);
        if (legendaries > MaxListItems) throw r.Broken();
        for (int i = 0; i < legendaries; i++) b.LegendaryItemIds.Add(Id(LegendaryBase + Unzigzag(r.EG(OffsetOrder)), r));
        b.WeaponAbilityId = ReadOptional(r, WeaponBase);
        r.End();
        return b;
    }

    private static double[] Stats(CharacterBuild b) =>
        [b.PrimaryStat, b.Stamina, b.Armor, b.CritRating, b.HasteRating, b.ExpertiseRating, b.SpiritRating];

    // A sorted list: its count, then each item's ID as its gap from the one before (the first's from 0), then what it holds.
    private static void WriteSorted<T>(BitWriter w, IEnumerable<T> items, Func<T, int> id, int firstOrder, int gapOrder, Action<T> rest)
    {
        var list = items.OrderBy(id).ToList();
        w.EG(list.Count, 0);
        int previous = 0;
        for (int i = 0; i < list.Count; i++)
        {
            int current = Whole(id(list[i]));
            w.EG(current - previous, i == 0 ? firstOrder : gapOrder);
            previous = current;
            rest(list[i]);
        }
    }

    private static void ReadSorted(BitReader r, int firstOrder, int gapOrder, Action<int> item)
    {
        long count = r.EG(0);
        if (count > MaxListItems) throw r.Broken();
        long id = 0;
        for (int i = 0; i < count; i++)
        {
            id += r.EG(i == 0 ? firstOrder : gapOrder);
            item(Id(id, r));
        }
    }

    // 0 for none, else 1 + the zigzag of the ID less the base.
    private static void WriteOptional(BitWriter w, int id, int baseId) => w.EG(id == 0 ? 0 : Zigzag(Whole(id) - baseId) + 1, OffsetOrder);

    private static int ReadOptional(BitReader r, int baseId)
    {
        long v = r.EG(OffsetOrder);
        return v == 0 ? 0 : Id(baseId + Unzigzag(v - 1), r);
    }

    private static int Rank(int rank) => rank >= 1 ? rank - 1 : throw Negative(rank);

    private static int Whole(int value) => value >= 0 ? value : throw Negative(value);

    private static long Number(double value, bool thousandths) =>
        value >= 0 && !double.IsNaN(value) ? (long)Math.Round(thousandths ? value * 1000 : value) : throw Negative(value);

    private static ArgumentOutOfRangeException Negative(object value) => new(nameof(value), value, "A share link holds no negative values or ranks below 1.");

    private static long Zigzag(long v) => v >= 0 ? 2 * v : -2 * v - 1;

    private static long Unzigzag(long v) => (v & 1) == 0 ? v / 2 : -(v + 1) / 2;

    private static int Id(long id, BitReader r) => id is >= 0 and <= int.MaxValue ? (int)id : throw r.Broken();

    private static GemColor Color(int c) => Enum.IsDefined(typeof(GemColor), c) ? (GemColor)c : throw new FormatException("The share link's code is not a build.");

    private sealed class BitWriter
    {
        private readonly List<bool> _bits = [];

        public void Bit(bool bit) => _bits.Add(bit);

        /// <summary>Exp-Golomb of order <paramref name="order"/>: v + 2^order in binary, after as many 0s as it has bits beyond order + 1.</summary>
        public void EG(long v, int order)
        {
            ulong x = (ulong)v + (1UL << order);
            int length = 64 - System.Numerics.BitOperations.LeadingZeroCount(x);
            for (int i = 0; i < length - 1 - order; i++) _bits.Add(false);
            for (int i = length - 1; i >= 0; i--) _bits.Add(((x >> i) & 1) == 1);
        }

        public string Text()
        {
            var s = new System.Text.StringBuilder((_bits.Count + 5) / 6);
            for (int i = 0; i < _bits.Count; i += 6)
            {
                int v = 0;
                for (int k = 0; k < 6; k++) v = (v << 1) | (i + k < _bits.Count && _bits[i + k] ? 1 : 0);
                s.Append(Alphabet[v]);
            }
            return s.ToString();
        }
    }

    private sealed class BitReader
    {
        private readonly List<bool> _bits = [];
        private int _at;

        public BitReader(string code)
        {
            foreach (char c in code)
            {
                int v = Alphabet.IndexOf(c);
                if (v < 0) throw new FormatException($"The share link's code has a character it cannot have ('{c}').");
                for (int k = 5; k >= 0; k--) _bits.Add(((v >> k) & 1) == 1);
            }
        }

        public bool Bit() => _at < _bits.Count ? _bits[_at++] : throw Broken();

        public long EG(int order)
        {
            int zeros = 0;
            while (!Bit())
            {
                if (++zeros > 40) throw Broken();
            }
            ulong x = 1;
            for (int i = 0; i < zeros + order; i++) x = (x << 1) | (Bit() ? 1UL : 0);
            return (long)(x - (1UL << order));
        }

        public int Int(int order)
        {
            long v = EG(order);
            return v <= int.MaxValue ? (int)v : throw Broken();
        }

        /// <summary>Only the 0 bits that fill the last character are left.</summary>
        public void End()
        {
            if (_bits.Count - _at >= 6 || _bits.Skip(_at).Any(b => b)) throw Broken();
        }

        public FormatException Broken() => new("The share link's code is cut short or is not a build.");
    }
}
