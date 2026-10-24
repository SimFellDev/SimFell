using System.Text.Json;
using FellowshipSim.Logs;
using Microsoft.Extensions.Configuration;

namespace FellowshipSim.UI.Services;

/// <summary>The FellowshipLogs API client credentials.</summary>
public sealed record LogsCredentials(string ClientId, string ClientSecret);

/// <summary>
/// Where the FellowshipLogs API credentials come from: the configuration keys FELLOWSHIPLOGS_CLIENT_ID and
/// FELLOWSHIPLOGS_CLIENT_SECRET if set (environment variables, or the <c>simfell</c> user secrets in development),
/// otherwise <c>settings.json</c> in the data folder (saved from the desktop app's Setup page; the data folder is never
/// committed).
/// </summary>
public sealed class LogsSettingsStore(ReplayLibrary library, IHttpClientFactory httpClients, IConfiguration configuration)
{
    public const string ClientIdKey = "FELLOWSHIPLOGS_CLIENT_ID";
    public const string ClientSecretKey = "FELLOWSHIPLOGS_CLIENT_SECRET";

    /// <summary>Name of the <see cref="HttpClient"/> of FellowshipLogs requests.</summary>
    public const string HttpClientName = "fellowshiplogs";

    public string SettingsPath => Path.Combine(library.DataDirectory, "settings.json");

    public LogsCredentials? Credentials
    {
        get
        {
            string? id = configuration[ClientIdKey];
            string? secret = configuration[ClientSecretKey];
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret)) return new LogsCredentials(id, secret);
            if (!File.Exists(SettingsPath)) return null;
            var saved = JsonSerializer.Deserialize<LogsCredentials>(File.ReadAllText(SettingsPath));
            return saved is { ClientId.Length: > 0, ClientSecret.Length: > 0 } ? saved : null;
        }
    }

    /// <summary>Raised after <see cref="Save"/> writes new credentials.</summary>
    public event Action? Saved;

    /// <summary>
    /// Writes the credentials to <see cref="SettingsPath"/>. They are the whole app's, so only the data folder's owner saves
    /// them; a web visitor throws <see cref="InvalidOperationException"/>, and the web app's host sets them in its
    /// environment.
    /// </summary>
    public void Save(Visitor visitor, LogsCredentials credentials)
    {
        if (!visitor.OwnsDataFolder) throw new InvalidOperationException("This app's FellowshipLogs API access is set by its host.");
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true }));
        Saved?.Invoke();
    }

    /// <summary>
    /// Saves the credentials after checking they can sign in. Throws <see cref="ArgumentException"/> when either is blank,
    /// and <see cref="FellowshipLogsException"/> when FellowshipLogs refuses them.
    /// </summary>
    public async Task SignInAndSaveAsync(Visitor visitor, string clientId, string clientSecret, CancellationToken ct = default)
    {
        if (!visitor.OwnsDataFolder) throw new InvalidOperationException("This app's FellowshipLogs API access is set by its host.");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new ArgumentException("Enter both the client ID and the client secret.");
        }
        var credentials = new LogsCredentials(clientId.Trim(), clientSecret.Trim());
        await Create(credentials).QueryAsync("query { __typename }", ct: ct);
        Save(visitor, credentials);
    }

    /// <summary>A client signed in with the stored credentials, or null when none are set.</summary>
    public FellowshipLogsClient? CreateClient() =>
        Credentials is { } c ? Create(c) : null;

    public FellowshipLogsClient Create(LogsCredentials c) => new(httpClients.CreateClient(HttpClientName), c.ClientId, c.ClientSecret);
}
