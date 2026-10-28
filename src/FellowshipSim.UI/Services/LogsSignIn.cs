using System.Text.Json;
using FellowshipSim.Logs;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The user signed in to FellowshipLogs (<see cref="FellowshipLogsSignIn"/>): their token, in <c>signin.json</c> in the
/// data folder (in the browser app, the browser's storage), renewed from its refresh token as it expires. Their requests
/// spend their own FellowshipLogs points first. The host's <see cref="ISignInLauncher"/> runs the sign-in itself; without
/// one, there is none (<see cref="CanSignIn"/>).
/// </summary>
public sealed class LogsSignIn(ReplayLibrary library, IHttpClientFactory httpClients, IServiceProvider services)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web) { WriteIndented = true };

    private ISignInLauncher? Launcher => services.GetService<ISignInLauncher>();

    private HttpClient Http => httpClients.CreateClient(LogsSettingsStore.HttpClientName);

    private string TokenPath => Path.Combine(library.DataDirectory, "signin.json");

    /// <summary>True when the host can sign a user in.</summary>
    public bool CanSignIn => Launcher != null;

    /// <summary>True while a user is signed in (their token may have to be renewed before use).</summary>
    public bool SignedIn => Load() != null;

    /// <summary>Raised when a user signs in or out.</summary>
    public event Action? Changed;

    /// <summary>
    /// Signs a user in from the page at <paramref name="returnTo"/>. In the browser app the page leaves for FellowshipLogs
    /// and the sign-in completes on its way back (<see cref="CompleteAsync"/>).
    /// </summary>
    public async Task SignInAsync(string returnTo, CancellationToken ct = default)
    {
        var launcher = Launcher ?? throw new InvalidOperationException("This app has no FellowshipLogs sign-in.");
        if (await launcher.SignInAsync(Http, returnTo, ct) is { } token) Save(token);
    }

    /// <summary>
    /// Completes the sign-in FellowshipLogs sent the page back from, at <paramref name="uri"/> (its code and state); returns
    /// the page to go back to.
    /// </summary>
    public async Task<string> CompleteAsync(Uri uri, CancellationToken ct = default)
    {
        var query = Query(uri.Query);
        var pending = Launcher is { } launcher ? await launcher.TakePendingAsync() : null;
        if (query.TryGetValue("error", out var error)) throw new FellowshipLogsException(error == "access_denied" ? "Signing in was cancelled." : $"FellowshipLogs did not sign you in ({error}).");
        if (pending == null || !query.TryGetValue("state", out var state) || state != pending.State || !query.TryGetValue("code", out var code))
        {
            throw new FellowshipLogsException("This sign-in did not start here. Sign in again.");
        }
        Save(await FellowshipLogsSignIn.ExchangeAsync(Http, code, pending.Verifier, pending.Redirect, ct));
        return pending.ReturnTo;
    }

    /// <summary>The signed-in user's access token, renewed when it has expired; null when no one is signed in (or the renewal fails).</summary>
    public async Task<string?> AccessTokenAsync(CancellationToken ct = default)
    {
        if (Load() is not { } token) return null;
        if (!token.IsExpired) return token.AccessToken;
        if (token.RefreshToken is { } refresh)
        {
            try
            {
                var renewed = await FellowshipLogsSignIn.RefreshAsync(Http, refresh, ct);
                Save(renewed);
                return renewed.AccessToken;
            }
            catch (FellowshipLogsException)
            {
                // The refresh token no longer works either: the user signs in again.
            }
        }
        SignOut();
        return null;
    }

    public void SignOut()
    {
        if (File.Exists(TokenPath)) File.Delete(TokenPath);
        Changed?.Invoke();
    }

    private LogsUserToken? Load()
    {
        try
        {
            return File.Exists(TokenPath) ? JsonSerializer.Deserialize<LogsUserToken>(File.ReadAllText(TokenPath), Json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Save(LogsUserToken token)
    {
        File.WriteAllText(TokenPath, JsonSerializer.Serialize(token, Json));
        Changed?.Invoke();
    }

    /// <summary>A query string's values by name ("?code=a&amp;state=b").</summary>
    internal static Dictionary<string, string> Query(string query) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(p => Uri.UnescapeDataString(p[0]))
            .ToDictionary(g => g.Key, g => g.First().Length > 1 ? Uri.UnescapeDataString(g.First()[1].Replace('+', ' ')) : "");
}
