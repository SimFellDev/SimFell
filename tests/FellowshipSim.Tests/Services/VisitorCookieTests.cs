using System.Net;
using FellowshipSim.Logs;
using FellowshipSim.Service;
using FellowshipSim.Service.Services;
using FellowshipSim.UI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FellowshipSim.Tests.Services;

/// <summary>
/// The web app (<see cref="ServiceApp"/> on a test server): the first request gives the browser a visitor cookie, the
/// cookie keeps that visitor on later requests (and outlives the browser), and a review's files are found only for the
/// visitor who ran it.
/// </summary>
public sealed class VisitorCookieTests : IAsyncLifetime
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"simfell-service-{Guid.NewGuid():N}");
    private WebApplication _app = null!;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        _app = ServiceApp.Build([$"--DataDirectory={_data}"], b => b.WebHost.UseTestServer());
        await _app.StartAsync();
        _http = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        if (Directory.Exists(_data)) Directory.Delete(_data, recursive: true);
    }

    [Fact]
    public async Task The_First_Request_Sets_A_Visitor_Cookie_And_The_Cookie_Keeps_The_Visitor()
    {
        string cookie = await NewVisitorCookieAsync();

        using var again = await GetAsync("/reviews/none.html", cookie);
        Assert.False(again.Headers.Contains("Set-Cookie"));
        Assert.Equal(VisitorId(cookie), VisitorId(cookie));
        Assert.Matches("^[0-9a-f]{32}$", VisitorId(cookie));
    }

    [Fact]
    public async Task A_Cookie_That_Ends_With_The_Browser_Is_Issued_Again_For_The_Same_Visitor()
    {
        var options = _app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(VisitorIdentity.ClaimType, "0123456789abcdef0123456789abcdef")], CookieAuthenticationDefaults.AuthenticationScheme));
        var session = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(user,
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        string cookie = $"{VisitorIdentity.CookieName}={Uri.EscapeDataString(options.TicketDataFormat.Protect(session))}";

        using var response = await GetAsync("/reviews/none.html", cookie);
        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(VisitorIdentity.CookieName + "="));
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("0123456789abcdef0123456789abcdef", VisitorId(setCookie.Split(';')[0]));
    }

    [Fact]
    public async Task A_Reviews_Files_Are_Found_Only_For_The_Visitor_Who_Ran_It()
    {
        string a = await NewVisitorCookieAsync(), b = await NewVisitorCookieAsync();
        Assert.NotEqual(VisitorId(a), VisitorId(b));
        var summary = new ReviewSummary(ReplayLibrary.ReviewId("aaaa", 1, 2), "aaaa", 1, "Fight", null, 2, "Player", "Rime",
            1, 1, 1, 1, 100, 100, 0, DateTime.UtcNow);
        _app.Services.GetRequiredService<ReplayLibrary>().Save(new ReviewArtifacts(summary, "{}"u8.ToArray(), "<html>review</html>", "", "hits", "issues", "{}"));
        await _app.Services.GetRequiredService<VisitorReviews>().AddAsync(new Visitor(VisitorId(a), OwnsDataFolder: false), summary);

        using (var page = await GetAsync($"/reviews/{summary.HtmlFile}", a))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal("<html>review</html>", await page.Content.ReadAsStringAsync());
        }
        using (var hits = await GetAsync($"/reviews/{summary.Id}.hits.csv", a)) Assert.Equal(HttpStatusCode.OK, hits.StatusCode);
        using (var other = await GetAsync($"/reviews/{summary.HtmlFile}", b)) Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using (var none = await GetAsync($"/reviews/{summary.HtmlFile}", null)) Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    /// <summary>The visitor cookie a request without one is given, as a Cookie header value.</summary>
    private async Task<string> NewVisitorCookieAsync()
    {
        using var response = await GetAsync("/reviews/none.html", null);
        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(VisitorIdentity.CookieName + "="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        // A cookie without an expiry ends with the browser, and the next visit would be a new visitor without their saves.
        string expires = Assert.Single(setCookie.Split(';').Select(p => p.Trim()), p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.True(DateTimeOffset.Parse(expires["expires=".Length..]) > DateTimeOffset.UtcNow.AddDays(399));
        return setCookie.Split(';')[0];
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie != null) request.Headers.Add("Cookie", cookie);
        return await _http.SendAsync(request);
    }

    /// <summary>The visitor ID in a visitor cookie, read with the app's own cookie keys.</summary>
    private string VisitorId(string cookie)
    {
        var options = _app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = options.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookie[(cookie.IndexOf('=') + 1)..]));
        return VisitorIdentity.IdOf(ticket!.Principal)!;
    }
}
