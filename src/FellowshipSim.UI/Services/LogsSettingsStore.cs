using FellowshipSim.Logs;
using Microsoft.Extensions.Configuration;

namespace FellowshipSim.UI.Services;

/// <summary>The FellowshipLogs API client credentials.</summary>
public sealed record LogsCredentials(string ClientId, string ClientSecret);

/// <summary>
/// The FellowshipLogs API credentials: the configuration keys FELLOWSHIPLOGS_CLIENT_ID and FELLOWSHIPLOGS_CLIENT_SECRET
/// (the host's environment, the <c>simfell</c> user secrets, or in development the repository's git-ignored
/// <c>settings.json</c>, which each app's build copies in). No page takes or saves them: for now only developers set an
/// API client (FellowshipLogs' terms keep one to its owner).
/// </summary>
public sealed class LogsSettingsStore(IHttpClientFactory httpClients, IConfiguration configuration)
{
    public const string ClientIdKey = "FELLOWSHIPLOGS_CLIENT_ID";
    public const string ClientSecretKey = "FELLOWSHIPLOGS_CLIENT_SECRET";

    /// <summary>Name of the <see cref="HttpClient"/> of FellowshipLogs requests.</summary>
    public const string HttpClientName = "fellowshiplogs";

    public LogsCredentials? Credentials
    {
        get
        {
            string? id = configuration[ClientIdKey];
            string? secret = configuration[ClientSecretKey];
            return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret) ? new LogsCredentials(id, secret) : null;
        }
    }

    /// <summary>A client signed in with the credentials, or null when none are set.</summary>
    public FellowshipLogsClient? CreateClient() =>
        Credentials is { } c ? Create(c) : null;

    public FellowshipLogsClient Create(LogsCredentials c) => new(httpClients.CreateClient(HttpClientName), c.ClientId, c.ClientSecret);
}
