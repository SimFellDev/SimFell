using System.Text.Json;
using FellowshipSim.Logs;
using FellowshipSim.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FellowshipSim.Web;

/// <summary>
/// The browser app's sign-in: the page goes to FellowshipLogs, which sends it back to /signin on this site (or on a local
/// build at 127.0.0.1:5000). The sign-in under way waits in the tab's session storage meanwhile.
/// </summary>
public sealed class BrowserSignInLauncher(IJSRuntime js, NavigationManager navigation) : ISignInLauncher
{
    private const string Key = "simfell.signin";

    public async Task<LogsUserToken?> SignInAsync(HttpClient http, string returnTo, CancellationToken ct)
    {
        string verifier = FellowshipLogsSignIn.NewVerifier(), state = FellowshipLogsSignIn.NewState();
        string redirect = navigation.BaseUri.StartsWith("http://127.0.0.1:5000/", StringComparison.Ordinal)
            ? FellowshipLogsSignIn.LocalSiteRedirect
            : FellowshipLogsSignIn.SiteRedirect;
        await js.InvokeVoidAsync("sessionStorage.setItem", ct, Key, JsonSerializer.Serialize(new PendingSignIn(verifier, state, redirect, returnTo)));
        navigation.NavigateTo(FellowshipLogsSignIn.AuthorizeUri(redirect, verifier, state), forceLoad: true);
        return null;
    }

    public async Task<PendingSignIn?> TakePendingAsync()
    {
        string? json = await js.InvokeAsync<string?>("sessionStorage.getItem", Key);
        await js.InvokeVoidAsync("sessionStorage.removeItem", Key);
        return json == null ? null : JsonSerializer.Deserialize<PendingSignIn>(json);
    }
}
