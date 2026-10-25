using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FellowshipSim.UI.Services;

/// <summary>A published version: its number, its tag (<c>v0.1.0</c>) and its download page.</summary>
public sealed record AppRelease(Version Version, string Tag, string Url);

/// <summary>
/// The packaged app's version and the latest one published: the releases of <see cref="ReleasesRepository"/> (public, so
/// no login is needed), each tagged <c>v</c> and its version. The latest is GitHub's "Latest" release (never a
/// pre-release), asked for once and kept; a failed check is no update. <paramref name="pretend"/> stands in for the
/// latest release without asking (to see the banner in testing).
/// </summary>
public sealed class AppUpdates(HttpClient http, Version? version, AppRelease? pretend = null)
{
    /// <summary>The public repository the builds are released to; the source repository is private.</summary>
    public const string ReleasesRepository = "SimFellDev/SimFell-Releases";

    /// <summary>The page of the latest release.</summary>
    public const string LatestReleasePage = "https://github.com/" + ReleasesRepository + "/releases/latest";

    private const string TagPrefix = "v";

    private Task<AppRelease?>? _latest;

    /// <summary>This app's version, or null for a build made outside the release workflow.</summary>
    public Version? Version { get; } = version;

    /// <summary>The latest published version, or null when there is none or it could not be read.</summary>
    public Task<AppRelease?> LatestAsync() => _latest ??= pretend != null ? Task.FromResult<AppRelease?>(pretend) : ReadLatestAsync();

    /// <summary>The latest version when it is newer than this one; null otherwise, and for a build with no version.</summary>
    public async Task<AppRelease?> NewerAsync() =>
        Version is { } version && await LatestAsync() is { } latest && latest.Version > version ? latest : null;

    /// <summary>A version as releases write it: major.minor.patch ("0.1.0"), or null.</summary>
    public static Version? Parse(string? text) =>
        System.Version.TryParse(text, out var v) && v.Build >= 0 && v.Revision < 0 ? v : null;

    /// <summary>The version of tag <paramref name="tag"/> (<c>v0.1.0</c>), or null.</summary>
    public static Version? VersionOf(string? tag) =>
        tag != null && tag.StartsWith(TagPrefix, StringComparison.Ordinal) ? Parse(tag[TagPrefix.Length..]) : null;

    /// <summary>The tag of version <paramref name="version"/>.</summary>
    public static string TagOf(Version version) => TagPrefix + version.ToString(3);

    private async Task<AppRelease?> ReadLatestAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{ReleasesRepository}/releases/latest");
            request.Headers.UserAgent.ParseAdd("SimFell");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
            return VersionOf(release?.TagName) is { } v ? new AppRelease(v, release!.TagName!, release.HtmlUrl ?? LatestReleasePage) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
