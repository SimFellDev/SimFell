using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Builds as share links and back (<see cref="BuildShareLink"/>).</summary>
public class BuildShareLinkTests
{
    private const string Site = "https://simfelldev.github.io/";

    private const string Profile = """
        elarion="TTV TheSpryFrog"
        primary_stat=161
        stamina=306
        armor=754
        crit_rating=206
        haste_rating=131
        expertise_rating=88
        spirit_rating=63
        talents=lunarlight_affinity/swift_reload/fervent_supremacy/final_crescendo/last_lights/resurgent_winds/lethal_shots/rising_moon
        aspects=brave_machinations:2/amethyst_splinters:4/visions_of_grandeur:3/emerald_judgement:1
        sigils=seized_opportunity:1/hunters_focus:1
        protective_enchant=the_mountain:1/the_dune:1/the_beetle:1
        glyphs=the_trickster:4/the_monarch:1
        gems=amethyst:500/emerald:1200/diamond:1000/sapphire:500/ruby:500
        gem_sockets=flawless_amethyst/flawless_emerald/flawless_diamond/flawless_sapphire
        gear_gem_bonuses=emerald:7/ruby:5/diamond:5
        stat_enchants=crit_haste:48/expertise_spirit:6/crit_expertise:6
        stat_enchant_counts=crit_haste:8/expertise_spirit:1/crit_expertise:1
        relic1=orb_of_hidden_riches,id=5921
        relic2=orb_of_awakened_gaze,id=5925
        legendary=shimmer,id=5899
        weapon=ashas_chronoshift_spire

        """;

    private static CharacterBuild Build() => SimcProfile.Parse(Profile).Build;

    /// <summary>The build's profile with every list in ID order: a link keeps a build's lists sorted.</summary>
    private static string Text(CharacterBuild build)
    {
        var b = SimcProfile.Parse(new SimcProfile(build, "").Format()).Build;
        b.Talents.Sort();
        b.Augmentations.Sort((x, y) => x.Id.CompareTo(y.Id));
        b.Glyphs.Sort((x, y) => x.Id.CompareTo(y.Id));
        b.SocketedGemIds.Sort();
        b.StatEnchants.Sort((x, y) => x.Id.CompareTo(y.Id));
        b.LegendaryItemIds.Sort();
        b.GemPower = b.GemPower.OrderBy(g => g.Key).ToDictionary();
        b.GearGemBonuses = b.GearGemBonuses.OrderBy(g => g.Key).ToDictionary();
        b.StatEnchantCounts = b.StatEnchantCounts.OrderBy(g => g.Key).ToDictionary();
        return new SimcProfile(b, "").Format();
    }

    [Fact]
    public void A_Link_Opens_The_Build_It_Was_Made_From()
    {
        var build = Build();
        string link = BuildShareLink.Create(Site, build);

        Assert.StartsWith($"{Site}build#elarion.", link);
        Assert.EndsWith(".TTV%20TheSpryFrog", link);
        // The code, between the hero and the name, is short.
        Assert.InRange(link.Split('#')[1].Split('.')[1].Length, 1, 80);
        var read = BuildShareLink.TryRead(link)!;
        Assert.Equal(Text(build), Text(read));
    }

    [Fact]
    public void Max_Health_And_Fractional_Stats_Survive()
    {
        var build = Build();
        build.MaxHealth = 27472;
        build.PrimaryStat = 161.25;
        build.StatsFromOldProfile = true;

        var read = BuildShareLink.TryRead(BuildShareLink.Create(Site, build))!;

        Assert.Equal(27472, read.MaxHealth);
        Assert.Equal(161.25, read.PrimaryStat);
        Assert.True(read.StatsFromOldProfile);
    }

    [Theory]
    [InlineData("Iunno")]
    [InlineData("玩奶妈玩的")]
    [InlineData("A.B. Build.")]
    [InlineData("50% crit ~ #2")]
    public void The_Name_Comes_Back_As_It_Was(string name)
    {
        var build = Build();
        build.Name = name;

        string link = BuildShareLink.Create(Site, build);

        Assert.False(link.EndsWith('.'));
        Assert.Equal(name, BuildShareLink.TryRead(link)!.Name);
    }

    [Fact]
    public void A_Link_Without_A_Name_Takes_The_Heros()
    {
        var build = Build();
        build.Name = "";
        string link = BuildShareLink.Create(Site, build);

        Assert.Equal(2, link[(link.IndexOf('#') + 1)..].Split('.').Length);
        Assert.Equal("Elarion", BuildShareLink.TryRead(link)!.Name);
    }

    [Fact]
    public void A_Link_Pasted_With_Text_Around_It_Reads()
    {
        string link = BuildShareLink.Create(Site, Build());

        var read = BuildShareLink.TryRead($"try this one\n{link} it's good");

        Assert.Equal(Text(Build()), Text(read!));
    }

    [Fact]
    public void Text_Without_A_Link_Is_No_Build() => Assert.Null(BuildShareLink.TryRead(Profile));

    [Theory]
    [InlineData("https://simfelldev.github.io/build#elarion")]
    [InlineData("https://simfelldev.github.io/build#nobody.AQ")]
    [InlineData("https://simfelldev.github.io/build#elarion.AQ")]
    [InlineData("https://simfelldev.github.io/build#elarion.!!!")]
    public void A_Broken_Link_Says_So(string link) => Assert.Throws<FormatException>(() => BuildShareLink.TryRead(link));

    [Fact]
    public void A_Link_Cut_Short_Says_So()
    {
        string link = BuildShareLink.Create(Site, Build());
        int dot = link.LastIndexOf('.');

        Assert.Throws<FormatException>(() => BuildShareLink.TryRead(link[..(dot - 4)]));
    }
}
