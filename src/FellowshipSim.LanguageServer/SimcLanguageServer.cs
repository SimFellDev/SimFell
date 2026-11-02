using System.Text.Json.Nodes;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The language server of .simc files: semantic tokens of each <see cref="AplTokenKind"/>, a diagnostic for the parse error
/// and for each name the hero does not have, and the action-list editor's completion list. Documents sync in full.
/// </summary>
public sealed class SimcLanguageServer(LspConnection connection)
{
    // The protocol's numbers (Language Server Protocol 3.17).
    private const int FullSync = 1;
    private const int ErrorSeverity = 1, WarningSeverity = 2;
    private const int InvokedTrigger = 1;
    private const int MethodNotFound = -32601;

    private readonly SimcLanguage _language = new();
    private readonly Dictionary<string, string> _documents = [];
    private bool _shutDown;

    /// <summary>Answers each message until <c>exit</c>; the exit code is 0 after <c>shutdown</c>, else 1.</summary>
    public async Task<int> RunAsync()
    {
        while (await connection.ReadAsync() is { } message)
        {
            string? method = (string?)message["method"];
            var id = message["id"]?.DeepClone();
            var parameters = message["params"];
            if (method == "exit") return _shutDown ? 0 : 1;
            try
            {
                var result = Handle(method, parameters, out bool known);
                if (id == null) continue;
                connection.Write(known
                    ? new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }
                    : Error(id, MethodNotFound, $"No method {method}."));
            }
            catch (Exception e)
            {
                // A message the server cannot answer leaves the server running: a request gets an error, a notification none.
                Console.Error.WriteLine($"{method}: {e}");
                if (id != null) connection.Write(Error(id, -32603, e.Message));
            }
        }
        return _shutDown ? 0 : 1;
    }

    private JsonNode? Handle(string? method, JsonNode? p, out bool known)
    {
        known = true;
        switch (method)
        {
            case "initialize":
                return Capabilities();
            case "shutdown":
                _shutDown = true;
                return null;
            case "textDocument/didOpen":
                Update((string)p!["textDocument"]!["uri"]!, (string)p["textDocument"]!["text"]!);
                return null;
            case "textDocument/didChange":
                Update((string)p!["textDocument"]!["uri"]!, (string)p["contentChanges"]!.AsArray()[^1]!["text"]!);
                return null;
            case "textDocument/didClose":
                string closed = (string)p!["textDocument"]!["uri"]!;
                _documents.Remove(closed);
                Publish(closed, []);
                return null;
            case "textDocument/semanticTokens/full":
                return SemanticTokens((string)p!["textDocument"]!["uri"]!);
            case "textDocument/completion":
                return Completion(p!);
            default:
                known = false;
                return null;
        }
    }

    private static JsonObject Capabilities() => new()
    {
        ["capabilities"] = new JsonObject
        {
            ["textDocumentSync"] = FullSync,
            ["completionProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray(".") },
            ["semanticTokensProvider"] = new JsonObject
            {
                ["legend"] = new JsonObject
                {
                    ["tokenTypes"] = new JsonArray([.. SimcLanguage.TokenTypes.Select(t => JsonValue.Create(t))]),
                    ["tokenModifiers"] = new JsonArray(),
                },
                ["full"] = true,
            },
        },
        ["serverInfo"] = new JsonObject { ["name"] = "SimFell" },
    };

    private void Update(string uri, string text)
    {
        _documents[uri] = text;
        Publish(uri, Diagnostics(text, _language.Check(PathOf(uri), text)));
    }

    private void Publish(string uri, JsonArray diagnostics) => connection.Write(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "textDocument/publishDiagnostics",
        ["params"] = new JsonObject { ["uri"] = uri, ["diagnostics"] = diagnostics },
    });

    /// <summary>The parse error as an error, and each token's problem as a warning: the editor's field error and warnings.</summary>
    public static JsonArray Diagnostics(string text, AplExpressionCheck? check)
    {
        var diagnostics = new JsonArray();
        if (check == null) return diagnostics;
        var lines = new LineMap(text);
        if (check.Error != null)
        {
            int at = check.ErrorStart ?? text.Length;
            diagnostics.Add(Diagnostic(lines, at, Math.Min(at + 1, text.Length), ErrorSeverity, check.Error));
        }
        foreach (var token in check.Tokens)
        {
            if (token.Problem != null) diagnostics.Add(Diagnostic(lines, token.Start, token.Start + token.Length, WarningSeverity, token.Problem));
        }
        return diagnostics;
    }

    private static JsonObject Diagnostic(LineMap lines, int from, int to, int severity, string message) => new()
    {
        ["range"] = Range(lines, from, to),
        ["severity"] = severity,
        ["source"] = "SimFell",
        ["message"] = message,
    };

    private JsonObject SemanticTokens(string uri)
    {
        string text = _documents.GetValueOrDefault(uri, "");
        return new JsonObject { ["data"] = new JsonArray([.. SemanticTokenData(text, _language.Check(PathOf(uri), text)).Select(n => JsonValue.Create(n))]) };
    }

    /// <summary>
    /// The protocol's relative encoding of the check's tokens: five numbers each, the line after the last token's, the
    /// start (after the last token's start on the same line), the length, the type's index and no modifiers.
    /// </summary>
    public static List<int> SemanticTokenData(string text, AplExpressionCheck? check)
    {
        var data = new List<int>();
        if (check == null) return data;
        var lines = new LineMap(text);
        int lastLine = 0, lastStart = 0;
        foreach (var token in check.Tokens.Where(t => t.Length > 0).OrderBy(t => t.Start))
        {
            var (line, start) = lines.PositionOf(token.Start);
            data.AddRange([line - lastLine, line == lastLine ? start - lastStart : start, token.Length, SimcLanguage.TokenIndex(token.Kind), 0]);
            (lastLine, lastStart) = (line, start);
        }
        return data;
    }

    private JsonObject? Completion(JsonNode p)
    {
        string uri = (string)p["textDocument"]!["uri"]!;
        string text = _documents.GetValueOrDefault(uri, "");
        var lines = new LineMap(text);
        int offset = lines.OffsetOf((int)p["position"]!["line"]!, (int)p["position"]!["character"]!);
        bool always = (int?)p["context"]?["triggerKind"] == InvokedTrigger;
        if (_language.Complete(PathOf(uri), text, offset, always) is not { } list) return null;
        var range = Range(lines, list.From, list.To);
        var items = new JsonArray();
        for (int i = 0; i < list.Items.Count; i++) items.Add(Item(list.Items[i], range, i));
        // Each part typed asks again: the list keeps the items containing the part, after those starting with it.
        return new JsonObject { ["isIncomplete"] = true, ["items"] = items };
    }

    private static JsonObject Item(Completion item, JsonObject range, int order)
    {
        var json = new JsonObject
        {
            ["label"] = item.Text,
            ["kind"] = CompletionKind(item.Node.Kind),
            ["sortText"] = order.ToString("D4"),
            ["filterText"] = item.Text,
            ["textEdit"] = new JsonObject { ["range"] = range.DeepClone(), ["newText"] = item.Written },
        };
        if (item.Node.Detail.Length > 0) json["detail"] = item.Node.Detail;
        // A pick with parts after it opens the list again on them.
        if (item.HasMore) json["command"] = new JsonObject { ["title"] = "Complete", ["command"] = "editor.action.triggerSuggest" };
        return json;
    }

    // The protocol's CompletionItemKind for the icon of each token kind.
    private static int CompletionKind(AplTokenKind? kind) => kind switch
    {
        AplTokenKind.Function => 3,
        AplTokenKind.Cooldown => 2,
        AplTokenKind.Keyword => 14,
        AplTokenKind.Operator => 24,
        AplTokenKind.List => 9,
        AplTokenKind.Resource => 12,
        _ => 6,
    };

    private static JsonObject Range(LineMap lines, int from, int to) => new() { ["start"] = Position(lines, from), ["end"] = Position(lines, to) };

    private static JsonObject Position(LineMap lines, int offset)
    {
        var (line, character) = lines.PositionOf(offset);
        return new JsonObject { ["line"] = line, ["character"] = character };
    }

    private static string? PathOf(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.IsFile ? u.LocalPath : null;

    private static JsonObject Error(JsonNode id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };
}
