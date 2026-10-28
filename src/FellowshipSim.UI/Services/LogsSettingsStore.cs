using System.Text.Json;
using FellowshipSim.Logs;
using Microsoft.Extensions.Configuration;

namespace FellowshipSim.UI.Services;

/// <summary>The FellowshipLogs API client credentials.</summary>
public sealed record LogsCredentials(string ClientId, string ClientSecret);

/// <summary>
/// Where the FellowshipLogs API credentials come from: the configuration keys FELLOWSHIPLOGS_CLIENT_ID and
/// FELLOWSHIPLOGS_CLIENT_SECRET if set (environment variables, the <c>simfell</c> user secrets in development, or the
/// browser app's git-ignored <c>appsettings.json</c>), otherwise <c>settings.json</c> in the data folder, which the owner
/// writes by hand (<see cref="ReadsSettingsFile"/>). The app saves none: FellowshipLogs' terms keep an API client to its
/// owner, so no page takes one.
/// </summary>
public sealed class LogsSettingsStore(ReplayLibrary library, IHttpClientFactory httpClients, IConfiguration configuration)
{
    public const string ClientIdKey = "FELLOWSHIPLOGS_CLIENT_ID";
    public const string ClientSecretKey = "FELLOWSHIPLOGS_CLIENT_SECRET";

    /// <summary>Name of the <see cref="HttpClient"/> of FellowshipLogs requests.</summary>
    public const string HttpClientName = "fellowshiplogs";

    /// <summary>False for an app that takes its credentials from configuration alone (the browser app, a public site).</summary>
    public bool ReadsSettingsFile { get; init; } = true;

    public string SettingsPath => Path.Combine(library.DataDirectory, "settings.json");

    public LogsCredentials? Credentials
    {
        get
        {
            string? id = configuration[ClientIdKey];
            string? secret = configuration[ClientSecretKey];
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret)) return new LogsCredentials(id, secret);
            if (!ReadsSettingsFile || !File.Exists(SettingsPath)) return null;
            var saved = JsonSerializer.Deserialize<LogsCredentials>(File.ReadAllText(SettingsPath));
            return saved is { ClientId.Length: > 0, ClientSecret.Length: > 0 } ? saved : null;
        }
    }

    /// <summary>A client signed in with the credentials, or null when none are set.</summary>
    public FellowshipLogsClient? CreateClient() =>
        Credentials is { } c ? Create(c) : null;

    public FellowshipLogsClient Create(LogsCredentials c) => new(httpClients.CreateClient(HttpClientName), c.ClientId, c.ClientSecret);
}
