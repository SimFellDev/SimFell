using System.Text.RegularExpressions;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// A review page has two stylesheets: its own for the saved file, and review.css in the app. Both show the app's colours.
/// </summary>
public class ReviewPageStyleTests
{
    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([TestPaths.RepositoryRoot, "src", "FellowshipSim.UI", "wwwroot", .. path]));

    private static string Token(string css, string name) =>
        Regex.Match(css, $@"--{name}:\s*([^;]+);").Groups[1].Value.Trim();

    [Theory]
    [InlineData("bg", "bg")]
    [InlineData("panel", "surface")]
    [InlineData("text", "text")]
    [InlineData("muted", "muted")]
    [InlineData("line", "border")]
    [InlineData("row-hover", "row-hover")]
    [InlineData("ok", "good")]
    [InlineData("bad", "bad")]
    [InlineData("warn", "warn")]
    [InlineData("accent", "accent")]
    [InlineData("accent-text", "accent-text")]
    [InlineData("heading", "heading")]
    [InlineData("heading-font", "heading-font")]
    public void The_Saved_Page_Has_The_Apps_Colours(string pageToken, string appToken)
    {
        string app = Token(Read("css", "app.css"), appToken);
        Assert.NotEmpty(app);
        Assert.Equal(app.Replace("\"", ""), Token(ReplayReportWriter.Render(new ReplayResult()), pageToken));
    }

    [Theory]
    [InlineData("tr.finding")]
    [InlineData("tr.finding-detail")]
    [InlineData("button.state")]
    [InlineData("tr.state-row")]
    [InlineData("td.wrap")]
    [InlineData(".timeline")]
    [InlineData("h3")]
    public void The_App_Styles_What_The_Page_Styles(string selector)
    {
        Assert.Contains(selector, ReplayReportWriter.Render(new ReplayResult()));
        Assert.Contains(selector, Read("review", "review.css"));
    }

    [Fact]
    public void The_App_Runs_The_Pages_Script_On_The_Pages_Markup()
    {
        Assert.Contains("window.reviewRoot", Read("review", "reviewView.js"));
        Assert.Contains("window.reviewRoot??document", ReplayReportWriter.Render(new ReplayResult { Issues = { new ReplayIssue(1, 1, IssueCategory.Casts, IssueEntity.Ability("Bolt"), "refused") } }));
    }
}
