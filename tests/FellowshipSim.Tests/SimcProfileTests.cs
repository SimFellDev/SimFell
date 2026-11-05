using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Reading and writing simc-style character profiles (<see cref="SimcProfile"/>).</summary>
public class SimcProfileTests
{
    private const string Profile = """
        rime="sww"
        primary_stat=300
        crit_rating=153
        haste_rating=148
        expertise_rating=0
        spirit_rating=0
        talents=avalanche/coalescing_frost/cold_shower/chilling_finesse/cascading_blitz/bursting_swallows/soulfrost_torrent/supreme_torrent/wisdom_of_the_north
        aspects=martial_initiative:2
        sigils=hunters_focus:1
        protective_enchant=the_oak:1
        glyphs=the_philosopher:4
        gems=sapphire:1500
        relic1=orb_of_gathering_storms,id=5924
        relic2=orb_of_awakened_gaze,id=5925
        legendary=frostwyrms_spite,id=5899
        weapon=ashas_chronoshift_spire

        actions+=/frost_bolt

        """;

    [Fact]
    public void Format_Writes_The_Text_It_Read()
    {
        Assert.Equal(Profile.ReplaceLineEndings(), SimcProfile.Parse(Profile).Format().ReplaceLineEndings());
    }

    [Fact]
    public void Source_Line_Is_The_Build_Log_Link_And_Is_Written_Back()
    {
        const string link = "https://www.fellowshiplogs.com/reports/V9QGwXzvZhPKMTaN?fight=7&source=64";
        string text = Profile.ReplaceLineEndings("\n").Replace("rime=\"sww\"\n", $"rime=\"sww\"\nsource={link}\n");
        Assert.Contains("source=", text);
        var parsed = SimcProfile.Parse(text);
        Assert.Equal(link, parsed.Build.LogSource);
        Assert.Equal(text.ReplaceLineEndings(), parsed.Format().ReplaceLineEndings());
        Assert.Equal("", SimcProfile.Parse(Profile).Build.LogSource);
        Assert.DoesNotContain("source=", SimcProfile.Parse(Profile).Format());
    }

    [Fact]
    public void Profile_Imports_Every_Talent_And_The_Helm()
    {
        var profile = SimcProfile.Parse(Profile);
        var hero = HeroImporter.Import(profile.Build);
        Assert.Equal(9, hero.Talents.Count);
        Assert.Contains(SoulfrostTorrent.TalentId, profile.Build.Talents);
        Assert.Equal([FrostwyrmsSpite.HelmItemId], profile.Build.LegendaryItemIds);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent") || f.StartsWith("Legendary"));
        Assert.Equal(StatDiminishingReturns.FromRating(148), hero.Stats.HastePercent, 9);
        Assert.Equal(StatBlock.BaseCritChance + StatDiminishingReturns.FromRating(153), hero.Stats.CritPercent, 9);
    }

    [Fact]
    public void An_Old_Percentage_Profile_Reads_As_It_Is_And_Is_Marked()
    {
        // Each percentage reads as the rating that gives it; the Main Stat as the gear's.
        var profile = SimcProfile.Parse("rime=\"x\"\nprimary_stat=300\ncrit=30%\nhaste=24%");
        Assert.True(profile.Build.StatsFromOldProfile);
        var hero = HeroImporter.Import(profile.Build);
        Assert.Equal(0.24, hero.Stats.HastePercent, 6);
        Assert.Equal(0.30, hero.Stats.CritPercent, 6);
        Assert.Equal(300 + StatBlock.BasePrimaryStat, hero.Stats.PrimaryStat);

        // It is written as ratings, still marked.
        var written = profile.Format();
        Assert.DoesNotContain("%", written);
        Assert.Contains("stats_from_old_profile=true", written);
        Assert.True(SimcProfile.Parse(written).Build.StatsFromOldProfile);
    }

    [Fact]
    public void Covetous_Greed_Raises_Every_Gem_Power()
    {
        var profile = SimcProfile.Parse("rime=\"x\"\ngems=sapphire:1000/ruby:400\nrelic1=orb_of_hidden_riches,id=5921");
        var hero = HeroImporter.Import(profile.Build);
        Assert.Equal([1250.0, 500.0], hero.Gems.Select(g => g.Power));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.24)]
    [InlineData(0.30)]
    [InlineData(0.45)]
    public void Each_Percentage_Has_The_Rating_That_Gives_It(double percent)
    {
        Assert.Equal(percent, StatDiminishingReturns.FromRating(StatDiminishingReturns.ToRating(percent)), 9);
    }

    [Fact]
    public void An_Unknown_Name_Names_Its_Line()
    {
        var error = Assert.Throws<FormatException>(() => SimcProfile.Parse("rime=\"x\"\ntalents=avalanche/not_a_talent"));
        Assert.Contains("line 2", error.Message);
        Assert.Contains("not_a_talent", error.Message);
    }

    [Fact]
    public void The_Action_List_Keeps_Its_Comments()
    {
        const string text = """
            # The profile's own comment.
            rime="sww"
            primary_stat=300

            # opener
            actions+=/ice_comet
            # filler
            #
            #   indented
            actions+=/frost_bolt
            # trailing
            """;
        var profile = SimcProfile.Parse(text);
        Assert.Equal("""
            # opener
            actions+=/ice_comet
            # filler
            #
            #   indented
            actions+=/frost_bolt
            # trailing

            """.ReplaceLineEndings(), profile.ActionList.ReplaceLineEndings());
        Assert.Equal(profile.ActionList, SimcProfile.Parse(profile.Format()).ActionList);
    }

    [Fact]
    public void A_Comment_Above_A_Build_Line_Is_Not_The_Action_Lists()
    {
        var profile = SimcProfile.Parse("rime=\"x\"\nactions+=/frost_bolt\n# the stats\nprimary_stat=300\n");
        Assert.Equal("actions+=/frost_bolt" + Environment.NewLine, profile.ActionList);
    }
}
