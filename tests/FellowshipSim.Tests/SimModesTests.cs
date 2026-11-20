using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests;

/// <summary>The Modes tab's fight settings (<see cref="FightOptions"/>) and finders (<see cref="BuildFinders"/>).</summary>
public class SimModesTests
{
    private const int Seconds = 60;
    private const int Fights = 40;

    private static CharacterBuild Rime() => new() { Hero = "Rime", Name = "Test", PrimaryStat = 1000, CritRating = 400 };

    private static MonteCarloResult Run(FightOptions fight, int targets = 1) =>
        new MonteCarloSimulator(Rime(), AplParser.ParseScript(DefaultActionLists.Read("Rime", DefaultActionLists.DefaultName)!))
            .Run(SimTime.FromSeconds(Seconds), Fights, targets, degreeOfParallelism: 2, keepFightDamage: true, fight: fight);

    [Fact]
    public void Fight_Lengths_Spread_Evenly_Over_The_Variance_And_Repeat_By_Seed()
    {
        var options = new FightOptions(LengthVariance: 0.2);
        var lengths = Enumerable.Range(1, 2000).Select(seed => options.Length(SimTime.FromSeconds(300), seed).TotalSeconds).ToList();
        Assert.All(lengths, l => Assert.InRange(l, 240, 360));
        Assert.InRange(lengths.Average(), 295, 305);
        Assert.True(lengths.Min() < 245 && lengths.Max() > 355);
        Assert.Equal(lengths[6], options.Length(SimTime.FromSeconds(300), 7).TotalSeconds);
        Assert.Equal(300, FightOptions.Default.Length(SimTime.FromSeconds(300), 7).TotalSeconds);
    }

    [Fact]
    public void A_Sim_Of_Varied_Lengths_Reports_Their_Mean_Length_And_Damage()
    {
        var fixedLength = Run(FightOptions.Default);
        var varied = Run(new FightOptions(LengthVariance: 0.3));
        Assert.Equal(Seconds, fixedLength.DurationSeconds);
        Assert.NotEqual(Seconds, varied.DurationSeconds);
        Assert.InRange(varied.DurationSeconds, Seconds * 0.85, Seconds * 1.15);
        Assert.Equal(varied.IterationDamage.Average(), varied.TotalMeanDamage, varied.TotalMeanDamage * 1e-9);
        Assert.NotEqual(fixedLength.MeanDps, varied.MeanDps);
    }

    [Fact]
    public void A_Targets_Health_Follows_The_Fights_Time()
    {
        var hero = HeroImporter.Import(Rime());
        var ctx = new SimulationContext(hero);
        var enemy = ctx.AddEnemy(101, 1, "Training Dummy 1", maxHealth: 1_000_000);
        enemy.FollowTime(ctx, SimTime.Zero, SimTime.FromSeconds(Seconds));
        Assert.Equal(100, enemy.HealthPercent, 6);
        ctx.RunUntil(SimTime.FromSeconds(45));
        Assert.Equal(25, enemy.HealthPercent, 6);
        // Damage leaves it alone.
        enemy.CurrentHealth = 1;
        Assert.Equal(25, enemy.HealthPercent, 6);
    }

    [Fact]
    public void Starting_Spirit_Changes_The_Fight()
    {
        Assert.NotEqual(Run(FightOptions.Default).MeanDps, Run(new FightOptions(StartingSpirit: 100)).MeanDps);
    }

    [Fact]
    public void Starting_Spirit_Stops_At_The_Builds_Most()
    {
        static double Dps(CharacterBuild build, double spirit) =>
            new MonteCarloSimulator(build, AplParser.ParseScript(DefaultActionLists.Read("Rime", DefaultActionLists.DefaultName)!))
                .Run(SimTime.FromSeconds(Seconds), Fights, degreeOfParallelism: 2, fight: new FightOptions(StartingSpirit: spirit)).MeanDps;

        // Without Ancestral Surge the most is 100, so 130 starts the same as 100.
        Assert.Equal(Dps(Rime(), 100), Dps(Rime(), 130));
        // Ancestral Surge - II (600 Sapphire) holds 130, and the extra 30 starts the fight.
        var sapphire = Rime();
        sapphire.GemPower[GemColor.Sapphire] = 600;
        Assert.Equal(130, HeroImporter.Import(sapphire).Resources.Get(FellowshipSim.Core.Domain.Hero.SpiritPoints).Max);
        Assert.NotEqual(Dps(sapphire, 100), Dps(sapphire, 130));
    }

    [Fact]
    public void The_Primary_Targets_Dps_Is_All_Of_One_Target_And_Part_Of_Several()
    {
        var single = Run(FightOptions.Default);
        Assert.Equal(single.TotalMeanDamage / Seconds, single.PrimaryTargetDps, single.PrimaryTargetDps * 1e-9);
        var aoe = Run(FightOptions.Default, targets: 4);
        Assert.InRange(aoe.PrimaryTargetDps, 1, aoe.MeanDps * 0.9);
    }

    [Fact]
    public void The_Talent_Finder_Tries_Each_Talent_That_Fits_The_Points_Left()
    {
        var build = Rime();
        var catalog = BuildCatalog.For("Rime");
        var variants = BuildFinders.Variants(build, FinderKind.Talents, FinderRanks.NextRank);
        Assert.Equal(catalog.Talents.Count(t => t.Talent.Modeled && t.Slot.Cost <= catalog.PointsLeft([])), variants.Count);
        Assert.All(variants, v => Assert.Single(v.Build.Talents));
        Assert.Empty(build.Talents);

        // With every point spent, nothing fits.
        var full = new BuildEditor(Rime());
        foreach (var talent in catalog.Talents) full.ToggleTalent(talent);
        Assert.All(BuildFinders.Variants(full.Build, FinderKind.Talents, FinderRanks.NextRank),
            v => Assert.True(catalog.PointsLeft(v.Build.Talents) >= 0));
    }

    [Fact]
    public void The_Rank_Finders_Raise_One_Choice_A_Rank_Or_To_Its_Highest()
    {
        var catalog = BuildCatalog.For("Rime");
        var next = BuildFinders.Variants(Rime(), FinderKind.Glyphs, FinderRanks.NextRank);
        var max = BuildFinders.Variants(Rime(), FinderKind.Glyphs, FinderRanks.MaxRank);
        Assert.Equal(catalog.Glyphs.Count(g => g.Modeled), next.Count);
        Assert.All(next, v => Assert.Equal(1, Assert.Single(v.Build.Glyphs).Rank));
        Assert.All(max, v => Assert.Equal(catalog.Glyphs.First(g => g.Id == v.Build.Glyphs[0].Id).MaxRank, v.Build.Glyphs[0].Rank));
    }

    [Fact]
    public void The_Relic_Finder_Tries_Every_Other_Pair()
    {
        int relics = BuildCatalog.For("Rime").Relics.Count;
        var build = Rime();
        var editor = new BuildEditor(build);
        var catalog = BuildCatalog.For("Rime");
        editor.Relic1 = catalog.Relics[0].ItemId;
        editor.Relic2 = catalog.Relics[1].ItemId;
        var variants = BuildFinders.Variants(build, FinderKind.Relics, FinderRanks.NextRank);
        Assert.Equal(relics * (relics - 1) / 2 - 1, variants.Count);
        Assert.All(variants, v => Assert.True(v.Build.Relic1ItemId != 0 && v.Build.Relic2ItemId != 0 && v.Build.Relic1ItemId != v.Build.Relic2ItemId));
    }

    [Fact]
    public void The_Gem_Finder_Gives_Each_Colour_The_Power_Of_Its_Next_Node()
    {
        var build = Rime();
        build.GemPower[GemColor.Amethyst] = 100;
        var amethyst = BuildFinders.Variants(build, FinderKind.Gems, FinderRanks.NextRank)
            .Single(v => v.Label.StartsWith("Amethyst", StringComparison.Ordinal));
        double next = Gem.Create(GemColor.Amethyst).Tree.Where(n => n.RequiredPower > 100).Min(n => n.RequiredPower);
        Assert.Equal(next, amethyst.Build.GemPower[GemColor.Amethyst]);

        // Every node: each node above the power, a run each, in order.
        var every = BuildFinders.Variants(build, FinderKind.Gems, FinderRanks.EveryRank)
            .Where(v => v.Label.StartsWith("Amethyst", StringComparison.Ordinal)).Select(v => v.Build.GemPower[GemColor.Amethyst]).ToList();
        Assert.Equal(Gem.Create(GemColor.Amethyst).Tree.Where(n => n.RequiredPower > 100).Select(n => n.RequiredPower).Order(), every);
        Assert.True(every.Count > 1);

        // Covetous Greed's bonus counts toward the node.
        build.Relic1ItemId = CovetousGreed.RelicItemId;
        var greedy = BuildFinders.Variants(build, FinderKind.Gems, FinderRanks.NextRank)
            .Single(v => v.Label.StartsWith("Amethyst", StringComparison.Ordinal));
        Assert.Equal(Math.Ceiling(next / CovetousGreed.GemPowerBonus), greedy.Build.GemPower[GemColor.Amethyst]);
    }
}
