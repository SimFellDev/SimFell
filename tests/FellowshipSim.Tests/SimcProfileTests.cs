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
        crit=30%
        haste=24%
        expertise=0%
        spirit=0%
        talents=avalanche/coalescing_frost/cold_shower/chilling_finesse/cascading_blitz/bursting_swallows/soulfrost_torrent/supreme_torrent/wisdom_of_the_north
        aspects=martial_initiative:2
        sigils=hunters_focus:1
        protective_enchant=the_oak:1
        glyphs=the_philosopher:4
        gems=sapphire:1500
        relic1=undying_heart,id=5924
        relic2=dragonbone_dice,id=5925
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
    public void Profile_Imports_Every_Talent_And_The_Helm()
    {
        var profile = SimcProfile.Parse(Profile);
        var hero = HeroImporter.Import(profile.Build);
        Assert.Equal(9, hero.Talents.Count);
        Assert.Contains(SoulfrostTorrent.TalentId, profile.Build.Talents);
        Assert.Equal([FrostwyrmsSpite.HelmItemId], profile.Build.LegendaryItemIds);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent") || f.StartsWith("Legendary"));
        Assert.Equal(0.24, hero.Stats.HastePercent, 6);
        Assert.Equal(0.30, hero.Stats.CritPercent, 6);
    }

    [Fact]
    public void Covetous_Greed_Raises_Every_Gem_Power()
    {
        var profile = SimcProfile.Parse("rime=\"x\"\ngems=sapphire:1000/ruby:400\nrelic1=covetous_greed,id=5921");
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
}
