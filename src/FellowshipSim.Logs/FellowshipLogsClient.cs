using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FellowshipSim.Logs;

/// <summary>
/// Client for the FellowshipLogs GraphQL API (https://www.fellowshiplogs.com). Authenticates with an API client's ID and
/// secret (client-credentials OAuth) and caches the access token until shortly before it expires; or, made with
/// <see cref="ForUser"/>, as a signed-in user, whose requests spend their own points first.
/// </summary>
public sealed class FellowshipLogsClient
{
    public const string TokenUrl = "https://www.fellowshiplogs.com/oauth/token";
    public const string ApiUrl = "https://www.fellowshiplogs.com/api/v2/client";

    /// <summary>The API for a signed-in user's token.</summary>
    public const string UserApiUrl = "https://www.fellowshiplogs.com/api/v2/user";

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private string? _token;
    private DateTime _tokenExpires;
    private readonly string _apiUrl = ApiUrl;

    public FellowshipLogsClient(HttpClient http, string clientId, string clientSecret)
    {
        _http = http;
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    /// <summary>A client that asks as the signed-in user whose token is <paramref name="accessToken"/>.</summary>
    public static FellowshipLogsClient ForUser(HttpClient http, string accessToken) => new(http, accessToken);

    private FellowshipLogsClient(HttpClient http, string accessToken)
    {
        _http = http;
        _clientId = _clientSecret = "";
        _token = accessToken;
        _tokenExpires = DateTime.MaxValue;
        _apiUrl = UserApiUrl;
    }

    /// <summary>Runs a GraphQL query and returns its <c>data</c> object; GraphQL errors are thrown.</summary>
    public async Task<JsonNode> QueryAsync(string query, object? variables = null, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("FellowshipSim/1.0");
        using var response = await _http.SendAsync(request, ct);
        string body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new FellowshipLogsException($"FellowshipLogs API returned {(int)response.StatusCode}: {Truncate(body)}");
        }
        var json = JsonNode.Parse(body) ?? throw new FellowshipLogsException("FellowshipLogs API returned an empty response.");
        if (json["errors"] is JsonArray { Count: > 0 } errors)
        {
            throw new FellowshipLogsException($"FellowshipLogs API error: {Truncate(errors.ToJsonString())}");
        }
        return json["data"] ?? throw new FellowshipLogsException("FellowshipLogs API response has no data.");
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token != null && DateTime.UtcNow < _tokenExpires) return _token;
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")])
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")));
        using var response = await _http.SendAsync(request, ct);
        string body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new FellowshipLogsException(
                $"Could not sign in to FellowshipLogs ({(int)response.StatusCode}). Check the API client ID and secret.");
        }
        var json = JsonNode.Parse(body);
        _token = json?["access_token"]?.GetValue<string>() ?? throw new FellowshipLogsException("FellowshipLogs returned no access token.");
        double lifetime = json["expires_in"]?.GetValue<double>() ?? 3600;
        _tokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(60, lifetime - 60));
        return _token;
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] + "..." : s;
}

public sealed class FellowshipLogsException(string message) : Exception(message);
