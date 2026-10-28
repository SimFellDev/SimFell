namespace FellowshipSim.Logs;

/// <summary>
/// A signed-in FellowshipLogs user's access token (<see cref="FellowshipLogsSignIn"/>): the requests it makes spend the
/// user's own points first. <see cref="RefreshToken"/>, when FellowshipLogs gives one, gets a new token once it expires.
/// </summary>
public sealed record LogsUserToken(string AccessToken, string? RefreshToken, DateTime ExpiresUtc)
{
    /// <summary>True from a minute before the token expires.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresUtc.AddMinutes(-1);
}
