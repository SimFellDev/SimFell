using FellowshipSim.Logs;

namespace FellowshipSim.Tests;

/// <summary>FellowshipLogs character pages (<see cref="CharacterLink"/>) and report links written back (<see cref="ReportLink.Url"/>).</summary>
public class LogLinkTests
{
    [Theory]
    [InlineData("https://www.fellowshiplogs.com/character/id/615", 615)]
    [InlineData("  fellowshiplogs.com/character/id/615/  ", 615)]
    [InlineData("https://www.fellowshiplogs.com/character/id/42?tab=rankings", 42)]
    public void Character_Page_Gives_Its_Id(string text, int id)
    {
        Assert.Equal(new CharacterLink(id), CharacterLink.TryParse(text));
    }

    [Theory]
    [InlineData("https://www.fellowshiplogs.com/reports/V9QGwXzvZhPKMTaN?fight=7&source=64")]
    [InlineData("V9QGwXzvZhPKMTaN")]
    [InlineData("https://www.fellowshiplogs.com/character/id/")]
    [InlineData("https://example.com/character/id/615")]
    public void Other_Text_Is_No_Character_Page(string text)
    {
        Assert.Null(CharacterLink.TryParse(text));
    }

    [Fact]
    public void Character_Page_Url_Reads_Back()
    {
        Assert.Equal(new CharacterLink(615), CharacterLink.TryParse(new CharacterLink(615).Url));
    }

    [Theory]
    [InlineData(7, 64, "https://www.fellowshiplogs.com/reports/ABC?fight=7&source=64")]
    [InlineData(7, null, "https://www.fellowshiplogs.com/reports/ABC?fight=7")]
    [InlineData(null, null, "https://www.fellowshiplogs.com/reports/ABC")]
    public void Report_Url_Has_Its_Fight_And_Player(int? fight, int? player, string url)
    {
        var link = new ReportLink("ABC", fight, player);
        Assert.Equal(url, link.Url);
        Assert.Equal(link, ReportLink.Parse(link.Url));
    }
}
