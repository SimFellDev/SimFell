using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// Which nodes of a gem colour are active at a given gem power: five slots, a rank I node at 80 / 150 / 220 / 300 / 450 gem power and a rank II
/// node at 600 / 800 / 1000 / 1250 / 1500 that replaces the rank I node of its slot; the five highest unlocked nodes are active.
/// Dev: every hero with every colour at the powers around each threshold (the whole power is what counts, so 449.99 is 449). The expected names
/// are written out here; <c>ranks</c> lists the rank in each slot (0 for no node in it). Only the node names are read: what a node does is in the
/// colour's own test class.
/// </summary>
public class GemNodeUnlocking
{
    private static readonly Dictionary<GemColor, string[]> SlotNames = new()
    {
        [GemColor.Ruby] = ["Might of the Minotaur", "Champion's Heart", "Unyielding Vitality", "Titan's Blood", "Blessing of the Conqueror"],
        [GemColor.Amethyst] = ["Sealed Fate", "Berserker's Zeal", "Reaper's Reprieve", "Killer Instinct", "Blessing of the Deathdealer"],
        [GemColor.Topaz] = ["Adrenaline Rush", "Thief's Alacrity", "Rogue's Resurgence", "Feline Grace", "Blessing of the Virtuoso"],
        [GemColor.Emerald] = ["First Strike", "Vanguard's Resolve", "Sentinel's Bastion", "Tactician's Accumen", "Blessing of the Commander"],
        [GemColor.Sapphire] = ["Ancestral Surge", "Mystic's Intuition", "Resonating Soul", "Oracle's Foresight", "Blessing of the Prophet"],
        [GemColor.Diamond] = ["Harmonious Soul", "Stoic's Teachings", "Tranquil Resolve", "Ancient's Wisdom", "Blessing of the Artisan"],
    };

    private static readonly (double Power, int[] Ranks)[] Powers =
    [
        (0.0, [0, 0, 0, 0, 0]),
        (79.0, [0, 0, 0, 0, 0]),
        (79.99, [0, 0, 0, 0, 0]),
        (80.0, [1, 0, 0, 0, 0]),
        (80.99, [1, 0, 0, 0, 0]),
        (149.0, [1, 0, 0, 0, 0]),
        (150.0, [1, 1, 0, 0, 0]),
        (219.0, [1, 1, 0, 0, 0]),
        (220.0, [1, 1, 1, 0, 0]),
        (299.0, [1, 1, 1, 0, 0]),
        (300.0, [1, 1, 1, 1, 0]),
        (449.0, [1, 1, 1, 1, 0]),
        (449.99, [1, 1, 1, 1, 0]),
        (450.0, [1, 1, 1, 1, 1]),
        (599.0, [1, 1, 1, 1, 1]),
        (600.0, [2, 1, 1, 1, 1]),
        (799.0, [2, 1, 1, 1, 1]),
        (800.0, [2, 2, 1, 1, 1]),
        (999.0, [2, 2, 1, 1, 1]),
        (1000.0, [2, 2, 2, 1, 1]),
        (1249.0, [2, 2, 2, 1, 1]),
        (1250.0, [2, 2, 2, 2, 1]),
        (1499.0, [2, 2, 2, 2, 1]),
        (1500.0, [2, 2, 2, 2, 2]),
        (2000.0, [2, 2, 2, 2, 2]),
    ];

    public static IEnumerable<object[]> Cases() =>
        from hero in GearFights.Heroes
        from color in Enum.GetValues<GemColor>()
        from p in Powers
        select new object[] { hero, color, p.Power, string.Join(",", p.Ranks) };

    private static string[] Expected(GemColor color, string ranks) =>
        ranks.Split(',').Select(int.Parse)
            .Select((rank, slot) => rank == 0 ? null : SlotNames[color][slot] + (rank == 2 ? " - II" : ""))
            .OfType<string>().ToArray();

    /// <summary>
    /// Player: a gem colour unlocks its nodes as its gem power grows: one per slot at 80, 150, 220, 300 and 450, each replaced by its rank II
    /// version at 600, 800, 1000, 1250 and 1500; below 80 no node is active, and only the whole part of the power counts.
    /// Dev: the active nodes' names in slot order against the list for that power, for every colour on every hero.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Nodes_Unlock_At_Their_Gem_Power(GearHero hero, GemColor color, double power, string ranks)
    {
        var f = GemFights.Fight(hero, color, power);
        if (power == 0.0)
        {
            Assert.Empty(f.Hero.Gems);
            return;
        }
        Assert.Equal(Expected(color, ranks), f.ActiveNodes());
    }

    /// <summary>
    /// Player: never more than five nodes are active, however much gem power there is.
    /// Dev: a colour at 5000 gem power has five nodes, all rank II.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void No_More_Than_Five_Nodes_Are_Active(GearHero hero)
    {
        foreach (var color in Enum.GetValues<GemColor>())
        {
            var nodes = GemFights.Fight(hero, color, 5000).ActiveNodes();
            Assert.Equal(5, nodes.Length);
            Assert.All(nodes, n => Assert.EndsWith(" - II", n));
        }
    }

    /// <summary>
    /// Player: gem colours are independent: two colours each unlock their own nodes from their own power.
    /// Dev: Ruby at 80 and Amethyst at 450.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Two_Colours_Unlock_Separately(GearHero hero)
    {
        var f = GemFights.FightWith(hero, (GemColor.Ruby, 80), (GemColor.Amethyst, 450));
        Assert.Equal(2, f.Hero.Gems.Count);
        Assert.Equal(["Might of the Minotaur"], f.Hero.Gems.Single(g => g.Color == GemColor.Ruby).Nodes.Select(n => n.Name));
        Assert.Equal(Expected(GemColor.Amethyst, "1,1,1,1,1"), f.Hero.Gems.Single(g => g.Color == GemColor.Amethyst).Nodes.Select(n => n.Name));
    }

    /// <summary>
    /// Player: no node of a gem is left out by the sim without saying so.
    /// Dev: at 5000 power the hero lists no unmodeled feature for any colour (the nodes that change only sheet ratings or health are modeled as doing nothing).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Every_Node_Is_Modeled(GearHero hero)
    {
        foreach (var color in Enum.GetValues<GemColor>())
        {
            var f = GemFights.Fight(hero, color, 5000);
            Assert.Empty(f.TheGem().UnmodeledNodes);
            Assert.DoesNotContain(f.Hero.UnmodeledFeatures, u => u.StartsWith("Gem node"));
        }
    }
}
