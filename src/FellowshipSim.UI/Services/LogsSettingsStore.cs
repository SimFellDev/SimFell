using FellowshipSim.Logs;
using Microsoft.Extensions.Configuration;

namespace FellowshipSim.UI.Services;

/// <summary>The FellowshipLogs API client credentials.</summary>
public sealed record LogsCredentials(string ClientId, string ClientSecret);

/// <summary>
/// The FellowshipLogs API credentials: the configuration keys FELLOWSHIPLOGS_CLIENT_ID and FELLOWSHIPLOGS_CLIENT_SECRET
/// (the host's environment, the <c>simfell</c> user secrets, or in development the repository's git-ignored
/// <c>settings.json</c>, which each app's build copies in). No page takes or saves them: for now only developers set an
/// API client (FellowshipLogs' terms keep one to its owner). A user signed in to FellowshipLogs (<see cref="LogsSignIn"/>)
/// comes first: their requests spend their own points.
/// </summary>
public sealed class LogsSettingsStore(IHttpClientFactory httpClients, IConfiguration configuration, LogsSignIn signIn)
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

    /// <summary>True when the app can ask FellowshipLogs: a user is signed in, or a developer's client is set.</summary>
    public bool HasAccess => signIn.SignedIn || Credentials != null;

    /// <summary>A client as the signed-in user, else with a developer's credentials; null when there is neither.</summary>
    public async Task<FellowshipLogsClient?> CreateClientAsync(CancellationToken ct = default) =>
        await signIn.AccessTokenAsync(ct) is { } token ? FellowshipLogsClient.ForUser(httpClients.CreateClient(HttpClientName), token)
        : Credentials is { } c ? Create(c) : null;

    public FellowshipLogsClient Create(LogsCredentials c) => new(httpClients.CreateClient(HttpClientName), c.ClientId, c.ClientSecret);
}
