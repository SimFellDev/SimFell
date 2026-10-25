using System.Net;
using System.Text;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>The packaged app's update check: the latest release of SimFellDev/SimFell-Releases against its own version.</summary>
public class AppUpdatesTests
{
    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("https://api.github.com/repos/SimFellDev/SimFell-Releases/releases/latest", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private const string Latest = """{ "tag_name": "v0.1.2", "html_url": "https://github.com/SimFellDev/SimFell-Releases/releases/tag/v0.1.2" }""";

    private static AppUpdates Updates(string? version, HttpStatusCode status = HttpStatusCode.OK, string body = Latest) =>
        new(new HttpClient(new Answer(status, body)), AppUpdates.Parse(version));

    [Fact]
    public async Task A_New_Patch_Is_An_Update_And_It_Asks_Once()
    {
        var answer = new Answer(HttpStatusCode.OK, Latest);
        var updates = new AppUpdates(new HttpClient(answer), new Version(0, 1, 1));
        var newer = await updates.NewerAsync();
        Assert.Equal(new AppRelease(new Version(0, 1, 2), "v0.1.2", "https://github.com/SimFellDev/SimFell-Releases/releases/tag/v0.1.2"), newer);
        await updates.NewerAsync();
        Assert.Equal(1, answer.Calls);
    }

    [Theory]
    [InlineData("0.1.2")]
    [InlineData("0.1.10")]
    [InlineData("0.2.0")]
    public async Task The_Latest_Version_Or_A_Newer_One_Is_Up_To_Date(string version) => Assert.Null(await Updates(version).NewerAsync());

    [Fact]
    public async Task Versions_Compare_By_Number_Not_Text()
    {
        Assert.NotNull(await Updates("0.0.9", body: """{ "tag_name": "v0.0.10" }""").NewerAsync());
        Assert.NotNull(await Updates("0.9.5", body: """{ "tag_name": "v0.10.0" }""").NewerAsync());
    }

    [Fact]
    public async Task No_Release_No_Version_Or_A_Failure_Shows_Nothing()
    {
        Assert.Null(await Updates("0.1.1", HttpStatusCode.NotFound, "{}").NewerAsync());
        Assert.Null(await Updates("0.1.1", body: "not json").NewerAsync());
        Assert.Null(await Updates(null).NewerAsync());
    }

    [Fact]
    public void Tags_Are_Versions()
    {
        Assert.Equal(new Version(0, 1, 2), AppUpdates.VersionOf("v0.1.2"));
        Assert.Null(AppUpdates.VersionOf("build-12"));
        Assert.Null(AppUpdates.Parse("0.1"));
        Assert.Equal("v1.0.0", AppUpdates.TagOf(new Version(1, 0, 0)));
    }
}
