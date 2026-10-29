using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// A gem's power changing during a fight (Anomalous Residue adds gem power for 60s) and the overcap bonus above 1500 gem power: the nodes follow the
/// new power at once, a lower node comes back when the power falls, and a new fight starts from the power the gem was bound with.
/// Dev: <see cref="Gem.ChangePower"/> on a bound hero, against a hero bound at the new power from the start: the stats the nodes change must be equal.
/// Heroes have no character sheet. The nodes' effects themselves are in the colour's own test class.
/// </summary>
public class GemLivePower
{
    private const double Tolerance = 1e-9;

    /// <summary>The first seven entries of <see cref="GemFights.StatVector"/>: Main Stat, haste, crit, crit against a healthy enemy, crit power, Expertise, Spirit.</summary>
    private const int Stats = 7;

    /// <summary>The power changes that cross no rating node's threshold (150 and 800), those are in <see cref="RatingChanges"/>.</summary>
    private static readonly (double From, double To)[] StatChangesList =
    [
        (79, 80), (219, 220), (299, 300), (449, 450), (599, 600), (999, 1000), (1249, 1250), (1499, 1500),
        (80, 79), (220, 219), (300, 299), (450, 449), (600, 599), (1000, 999), (1250, 1249), (1500, 1499),
        (1000, 1250), (1250, 1000), (300, 449), (600, 799),
    ];

    public static IEnumerable<object[]> Changes() =>
        from hero in GearFights.Heroes
        from color in Enum.GetValues<GemColor>()
        from change in StatChangesList
        select new object[] { hero, color, change.From, change.To };

    /// <summary>
    /// Player: when a gem's power changes in a fight, you have the nodes of the new power: the stats are those of a hero who had that power all along.
    /// Dev: main stat, haste, crit (also against a full-health enemy), crit power, Expertise and Spirit of a hero bound at <c>from</c> and changed to
    /// <c>to</c> at 5s (a Ruby node reached in the fight gets its aura applied, as a replay's log does), against a hero bound at <c>to</c> (at 5s too), for changes that cross no flat rating node (150 and 800: see
    /// <see cref="A_Rating_Node_Reached_In_A_Fight_Adds_Its_Rating"/>). The cooldown, Spirit cost and Spirit Points maximum are in their own tests.
    /// </summary>
    [Theory]
    [MemberData(nameof(Changes))]
    public void A_Power_Change_Gives_The_Stats_Of_The_New_Power(GearHero hero, GemColor color, double from, double to)
    {
        var changed = GemFights.Fight(hero, color, from);
        changed.Wait(5);
        var before = changed.TheGem().Nodes;
        changed.TheGem().ChangePower(to, changed.Now);
        // Ruby's situational buffs (health, boss) are not guessed: a node reached in a fight gets its aura as the log gives it in a replay.
        if (color == GemColor.Ruby)
            foreach (var node in changed.TheGem().Nodes.Where(n => n.Grants != null && before.All(b => b.Slot != n.Slot)))
                changed.Hero.Auras.Apply(node.Grants!, changed.Now);
        var fresh = GemFights.Fight(hero, color, to);
        fresh.Wait(5);
        Assert.Equal(fresh.ActiveNodes(), changed.ActiveNodes());
        var expected = fresh.StatVector();
        var actual = changed.StatVector();
        for (int i = 0; i < Stats; i++)
            Assert.True(Math.Abs(expected[i] - actual[i]) < Tolerance, $"{GemFights.StatNames[i]}: a hero bound at {to} has {expected[i]}, one changed from {from} has {actual[i]}");
    }

    public static IEnumerable<object[]> RatingChanges() =>
        from hero in GearFights.Heroes
        from node in new (GemColor Color, double Low, double High, int Stat)[]
        {
            (GemColor.Amethyst, 149, 150, 2), (GemColor.Amethyst, 799, 800, 2),
            (GemColor.Topaz, 149, 150, 1), (GemColor.Topaz, 799, 800, 1),
            (GemColor.Emerald, 149, 150, 5), (GemColor.Emerald, 799, 800, 5),
            (GemColor.Sapphire, 149, 150, 6), (GemColor.Sapphire, 799, 800, 6),
        }
        select new object[] { hero, node.Color, node.Low, node.High, node.Stat };

    /// <summary>
    /// Player: a gem's flat rating node (Berserker's Zeal, Thief's Alacrity, Vanguard's Resolve, Mystic's Intuition) reached or lost during a fight changes
    /// the stat: more of it with the node, less without.
    /// Dev: the stat after raising the power across 150 (and the rank II step at 800) is higher than before, and back to the old value when it falls again.
    /// The exact size is not asserted (it is each colour's own test: a hero built from gem power has the rating from the start).
    /// </summary>
    [Theory]
    [MemberData(nameof(RatingChanges))]
    public void A_Rating_Node_Reached_In_A_Fight_Adds_Its_Rating(GearHero hero, GemColor color, double low, double high, int stat)
    {
        var f = GemFights.Fight(hero, color, low);
        double before = f.StatVector()[stat];
        f.TheGem().ChangePower(high, f.Now);
        double during = f.StatVector()[stat];
        Assert.True(during > before, $"{GemFights.StatNames[stat]} did not rise: {before} then {during}");
        f.TheGem().ChangePower(low, f.Now);
        Assert.Equal(before, f.StatVector()[stat], 1e-9);
    }

    /// <summary>
    /// Player: Blessing of the Commander's cooldown reduction follows the gem's power during a fight (4% from 450, 12% from 1500, back down when it falls).
    /// Dev: the Major ability's cooldown on a hero bound at 449 and raised to 450 at 5s is the same as one bound at 450 (and 1499 to 1500, and falling).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void A_Power_Change_Moves_The_Cooldown_Reduction(GearHero hero)
    {
        foreach (var (from, to) in new[] { (449.0, 450.0), (1499.0, 1500.0), (450.0, 449.0), (1500.0, 1499.0) })
        {
            var changed = GemFights.Fight(hero, GemColor.Emerald, from);
            changed.Wait(5);
            changed.TheGem().ChangePower(to, changed.Now);
            var fresh = GemFights.Fight(hero, GemColor.Emerald, to);
            fresh.Wait(5);
            Assert.Equal(fresh.MajorCooldown(hero), changed.MajorCooldown(hero), 0.001);
        }
    }

    /// <summary>
    /// Player: Blessing of the Prophet's cheaper Spirit ability and longer Spirit of Heroism follow the gem's power during a fight.
    /// Dev: a hero bound at 449 and raised to 450 at 5s can cast the Spirit ability with 95 Spirit Points and has Spirit of Heroism for 26 seconds.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void A_Power_Change_Moves_The_Spirit_Ability_Cost_And_Duration(GearHero hero)
    {
        var f = GemFights.Fight(hero, GemColor.Sapphire, 449);
        f.Wait(5);
        f.TheGem().ChangePower(450, f.Now);
        GearFights.Fill(f);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 95;
        Assert.True(f.TryCast(hero.Get(f, hero.Spirit)).Succeeded);
        f.Wait(60);
        var gained = f.Auras(StepEventKind.AuraGained, "Spirit of Heroism").First();
        var removed = f.Auras(StepEventKind.AuraRemoved, "Spirit of Heroism").First();
        Assert.Equal(26.0, (removed.Time - gained.Time).TotalSeconds, 0.05);
    }

    /// <summary>
    /// Player: Ancestral Surge's extra Spirit Points go when the power falls below 80 (and not before).
    /// Dev: maximum Spirit Points 110 at 80, then 100 after the power falls to 79.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Losing_Ancestral_Surge_Lowers_Maximum_Spirit_Points(GearHero hero)
    {
        var f = GemFights.Fight(hero, GemColor.Sapphire, 80);
        Assert.Equal(110.0, f.Hero.Resources.Get(Hero.SpiritPoints).Max);
        f.TheGem().ChangePower(79, f.Now);
        Assert.Equal(100.0, f.Hero.Resources.Get(Hero.SpiritPoints).Max);
    }

    private sealed class ApproximateComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < Tolerance;
        public int GetHashCode(double value) => 0;
    }

    /// <summary>
    /// Player: when the gem's power falls back (the Anomalous Residue wears off) a lower node comes back in the slot a rank II node left.
    /// Dev: Amethyst bound at 1500, 1500 to 1499 and the Deathdealer node is rank I again (crit power x1.02, not x1.06), Killer Instinct stays rank II.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void The_Lower_Node_Comes_Back_When_The_Power_Falls(GearHero hero)
    {
        var f = GemFights.Fight(hero, GemColor.Amethyst, 1500);
        double power = f.CritPower();
        double crit = f.Crit();
        f.TheGem().ChangePower(1499, f.Now);
        Assert.Equal(power / 1.06 * 1.02, f.CritPower(), Tolerance);
        Assert.Equal(crit, f.Crit(), Tolerance);
        Assert.Contains("Blessing of the Deathdealer", f.ActiveNodes());
    }

    /// <summary>
    /// Player: a new fight starts with the power the gem had when you put it on, whatever the last fight did to it.
    /// Dev: a gem bound at 450 raised to 1500 in a fight, then the hero is reset for a new fight: the gem's power, its nodes and the stats are those at 450.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void A_New_Fight_Starts_From_The_Bound_Power(GearHero hero)
    {
        foreach (var color in Enum.GetValues<GemColor>())
        {
            var f = GemFights.Fight(hero, color, 450);
            f.TheGem().ChangePower(1500, f.Now);
            f.Hero.ResetForIteration();
            var next = new StepDriver(f.Hero, 1, 1);
            var reference = GemFights.Fight(hero, color, 450);
            Assert.Equal(450.0, f.TheGem().Power);
            Assert.Equal(reference.ActiveNodes(), next.ActiveNodes());
            Assert.Equal(reference.StatVector(), next.StatVector(), new ApproximateComparer());
        }
    }

    /// <summary>
    /// Player: gem power over 1500 adds 0.005% Main Stat for each point over it (the bonus counts the power over the cap, summed over the colours).
    /// Dev: a hero with Amethyst and Topaz (neither changes Main Stat) bound at 1500 and raised to 1600 and 1700: Main Stat x1.005 and x1.01; and both
    /// colours at 1600: the two bonuses add (x1.01).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Gem_Power_Over_The_Cap_Adds_Main_Stat_In_A_Fight(GearHero hero)
    {
        var f = GemFights.FightWith(hero, (GemColor.Amethyst, 1500));
        double main = f.Main();
        f.TheGem().ChangePower(1600, f.Now);
        Assert.Equal(1.005, f.Main() / main, 1e-9);
        f.TheGem().ChangePower(1700, f.Now);
        Assert.Equal(1.01, f.Main() / main, 1e-9);
        f.TheGem().ChangePower(1500, f.Now);
        Assert.Equal(1.0, f.Main() / main, 1e-9);

        var both = GemFights.FightWith(hero, (GemColor.Amethyst, 1500), (GemColor.Topaz, 1500));
        double bothMain = both.Main();
        foreach (var gem in both.Hero.Gems) gem.ChangePower(1600, both.Now);
        Assert.Equal(1.01, both.Main() / bothMain, 1e-9);
    }

    /// <summary>
    /// Player: a hero put on gems over the cap from the start has the overcap bonus too.
    /// Dev: Amethyst bound at 1600 (no sheet, so the engine has to add it): Main Stat x1.005 over the same hero at 1500. The engine adds it only when the
    /// power changes during a fight; a character sheet has it in its numbers, but a hero built from gem power does not get it.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Gem_Power_Over_The_Cap_Adds_Main_Stat_When_Bound(GearHero hero)
    {
        Assert.Equal(1.005, GemFights.Fight(hero, GemColor.Amethyst, 1600).Main() / GemFights.Fight(hero, GemColor.Amethyst, 1500).Main(), 1e-9);
    }
}
