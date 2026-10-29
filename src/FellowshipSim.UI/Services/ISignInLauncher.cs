using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// How a host signs a user in to FellowshipLogs (<see cref="LogsSignIn"/>). The browser app sends the page to
/// FellowshipLogs, which sends it back to /signin; the desktop and local apps open the browser and catch the way back on
/// this computer (<see cref="LoopbackSignInLauncher"/>). A host without one (the hosted web app) has no sign-in.
/// </summary>
public interface ISignInLauncher
{
    /// <summary>
    /// Starts a sign-in to SimFell's client <paramref name="clientId"/> from the page at <paramref name="returnTo"/>.
    /// Returns the user's token when the sign-in completes in this call; null when the page goes to FellowshipLogs and the
    /// sign-in completes where it comes back.
    /// </summary>
    Task<LogsUserToken?> SignInAsync(HttpClient http, string clientId, string returnTo, CancellationToken ct);

    /// <summary>The sign-in this host started and sent the page away for, taken (so it completes once); null when none.</summary>
    Task<PendingSignIn?> TakePendingAsync();
}

/// <summary>A sign-in under way: its PKCE verifier and state, the address it comes back to, and the page to go back to.</summary>
public sealed record PendingSignIn(string Verifier, string State, string Redirect, string ReturnTo);
