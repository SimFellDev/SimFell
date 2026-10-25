using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.Logs;

namespace FellowshipSim.Service.Services;

/// <summary>
/// The engine server (FellowshipSim.EngineServer) over HTTP. Its address is the service "engine" (from service discovery,
/// "services:engine:http:0" in configuration) unless the client already has a base address. Each request has the shared
/// key ("EngineKey") as its bearer token. A 400 reply throws <see cref="SimEngineException"/> with the server's
/// <c>error</c>; any other failure throws <see cref="HttpRequestException"/>.
/// </summary>
public sealed class RemoteSimEngine : ISimEngine
{
    /// <summary>
    /// Longest wait for a reply to any request but a sim. A review of a long fight takes minutes. A sim has no limit: it
    /// runs until its result or until its caller cancels it.
    /// </summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private readonly HttpClient _http;

    /// <param name="http">A client with no timeout (<see cref="Timeout.InfiniteTimeSpan"/>), so a sim can run for hours.</param>
    public RemoteSimEngine(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("http://engine");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            configuration["EngineKey"] ?? throw new InvalidOperationException("Set EngineKey: the key the engine expects with each request."));
    }

    public async Task<IReadOnlyList<string>> GetHeroesAsync(CancellationToken ct = default) =>
        (await SendAsync<List<string>>(HttpMethod.Get, "heroes", null, ct))!;

    public Task<AplVocabulary?> GetVocabularyAsync(string hero, CancellationToken ct = default) =>
        SendAsync<AplVocabulary>(HttpMethod.Get, $"apl/heroes/{Uri.EscapeDataString(hero)}/vocabulary", null, ct, notFoundIsNull: true);

    public Task<AplExpressionCheck?> CheckExpressionAsync(string hero, string text, CancellationToken ct = default) =>
        SendAsync<AplExpressionCheck>(HttpMethod.Post, $"apl/heroes/{Uri.EscapeDataString(hero)}/check",
            JsonContent.Create(new AplText(text), options: Json), ct, notFoundIsNull: true);

    public async Task<AplDocumentActions> ParseActionListAsync(string text, CancellationToken ct = default) =>
        (await SendAsync<AplDocumentActions>(HttpMethod.Post, "apl/parse", JsonContent.Create(new AplText(text), options: Json), ct))!;

    public async Task<AplDocumentText> WriteActionListAsync(IReadOnlyList<AplDocumentAction> actions, CancellationToken ct = default) =>
        (await SendAsync<AplDocumentText>(HttpMethod.Post, "apl/write",
            JsonContent.Create(new AplDocumentActions(actions), options: Json), ct))!;

    /// <summary>
    /// Sends <paramref name="rawFight"/> as the request body. The endpoint reports no progress, so
    /// <paramref name="progress"/> receives nothing.
    /// </summary>
    public async Task<ReviewArtifacts> ReviewAsync(Stream rawFight, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var content = new StreamContent(rawFight);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return (await SendAsync<ReviewArtifacts>(HttpMethod.Post, $"reviews?playerId={playerId}", content, ct))!;
    }

    /// <summary>
    /// Reads the <c>POST sims</c> stream one <see cref="SimStreamLine"/> at a time. Cancelling <paramref name="ct"/>
    /// closes the connection, and the server stops the sim.
    /// </summary>
    public async Task<SimResult> SimulateAsync(SimcProfile profile, SimSettings settings, IProgress<SimProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "sims")
            {
                Content = JsonContent.Create(new SimRequest(profile, settings), options: Json),
            };
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            await EnsureAcceptedAsync(response, ct);
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(body);
            while (await reader.ReadLineAsync(ct) is { } text)
            {
                if (text.Length == 0) continue;
                var line = JsonSerializer.Deserialize<SimStreamLine>(text, SimStreamLine.Json);
                if (line?.Error is { } error) throw new SimEngineException(error);
                if (line?.Result is { } result) return result;
                if (line?.Progress is { } report) progress?.Report(report);
            }
            throw new HttpRequestException("The engine server ended the sim's stream with no result.");
        }
        catch (Exception e) when (ct.IsCancellationRequested && e is not OperationCanceledException)
        {
            // A read that the closed connection broke, not a failure of the sim.
            throw new OperationCanceledException(null, e, ct);
        }
    }

    /// <summary>
    /// The reply's JSON body as <typeparamref name="T"/>; null for a 404 when <paramref name="notFoundIsNull"/>. The wait
    /// ends after <see cref="RequestTimeout"/> with a <see cref="TimeoutException"/>.
    /// </summary>
    private async Task<T?> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct,
        bool notFoundIsNull = false) where T : class
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        // The request is not disposed. Disposing it disposes a review's fight stream, and the caller owns that stream.
        var request = new HttpRequestMessage(method, path) { Content = content };
        try
        {
            using var response = await _http.SendAsync(request, timeout.Token);
            if (notFoundIsNull && response.StatusCode == HttpStatusCode.NotFound) return null;
            await EnsureAcceptedAsync(response, timeout.Token);
            return await response.Content.ReadFromJsonAsync<T>(Json, timeout.Token)
                ?? throw new HttpRequestException($"The engine server sent an empty reply to {path}.");
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"The engine server sent no reply to {path} in {RequestTimeout.TotalMinutes} minutes.", e);
        }
    }

    /// <summary>Throws for a reply other than success: <see cref="SimEngineException"/> for a 400, otherwise <see cref="HttpRequestException"/>.</summary>
    private static async Task EnsureAcceptedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? error = await ErrorOfAsync(response, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest && error != null) throw new SimEngineException(error);
        throw new HttpRequestException($"The engine server refused the request ({(int)response.StatusCode}){(error == null ? "." : $": {error}")}",
            null, response.StatusCode);
    }

    /// <summary>The <c>error</c> member of a reply's JSON body, or null when the body has none.</summary>
    private static async Task<string?> ErrorOfAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string text = await response.Content.ReadAsStringAsync(ct);
        try
        {
            return JsonNode.Parse(text)?["error"]?.GetValue<string>();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
