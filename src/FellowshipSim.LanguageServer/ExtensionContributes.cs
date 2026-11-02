using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FellowshipSim.Core.APL;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The parts of the VS Code extension's package.json the language server and the app decide: a semantic token type for each
/// <see cref="AplTokenKind"/> (<c>contributes.semanticTokenTypes</c>), and each type's colour, italic and underline in a
/// .simc file from its <c>.tok-&lt;kind&gt;</c> rule in FellowshipSim.UI's design-system.css, with app.css's custom properties
/// (<c>contributes.configurationDefaults</c>, <c>editor.semanticTokenColorCustomizations</c>).
/// </summary>
public static partial class ExtensionContributes
{
    public const string Language = "simc";

    [GeneratedRegex(@"\.tok-([a-z]+)\s*\{([^}]*)\}")]
    private static partial Regex TokenRule();

    [GeneratedRegex(@"([a-z-]+)\s*:\s*([^;]+);")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"(--[a-z0-9-]+)\s*:\s*([^;]+);")]
    private static partial Regex CustomProperty();

    [GeneratedRegex(@"^var\((--[a-z0-9-]+)\)$")]
    private static partial Regex Var();

    /// <summary>Writes <see cref="Of"/> into the package.json at <paramref name="packageJson"/>.</summary>
    public static void Write(string packageJson, string cssFolder)
    {
        var package = JsonNode.Parse(File.ReadAllText(packageJson))!.AsObject();
        Apply(package, cssFolder);
        File.WriteAllText(packageJson, package.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 2 }) + "\n");
    }

    /// <summary>Sets <paramref name="package"/>'s token types and token colours.</summary>
    public static void Apply(JsonObject package, string cssFolder)
    {
        var (types, rules) = Of(cssFolder);
        var contributes = package["contributes"]!.AsObject();
        contributes["semanticTokenTypes"] = types;
        var defaults = contributes["configurationDefaults"] as JsonObject ?? [];
        defaults["editor.semanticTokenColorCustomizations"] = new JsonObject { ["rules"] = rules };
        contributes["configurationDefaults"] = defaults;
    }

    /// <summary>The token types, and the rules of each type with a <c>.tok-&lt;kind&gt;</c> rule.</summary>
    public static (JsonArray Types, JsonObject Rules) Of(string cssFolder)
    {
        string appCss = File.ReadAllText(Path.Combine(cssFolder, "app.css"));
        string designSystem = File.ReadAllText(Path.Combine(cssFolder, "design-system.css"));
        var properties = CustomProperty().Matches(appCss).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
        var classes = TokenRule().Matches(designSystem).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

        var types = new JsonArray();
        var rules = new JsonObject();
        foreach (var kind in Enum.GetValues<AplTokenKind>())
        {
            string type = SimcLanguage.TokenType(kind);
            types.Add(new JsonObject { ["id"] = type, ["description"] = $"An action list's {kind.ToString().ToLowerInvariant()} token." });
            if (!classes.TryGetValue(kind.ToString().ToLowerInvariant(), out string? body)) continue;

            var style = new JsonObject();
            foreach (Match declaration in Declaration().Matches(body))
            {
                string value = declaration.Groups[2].Value.Trim();
                switch (declaration.Groups[1].Value)
                {
                    case "color": style["foreground"] = Resolve(value, properties); break;
                    case "font-style" when value == "italic": style["italic"] = true; break;
                    case "text-decoration" when value.StartsWith("underline", StringComparison.Ordinal): style["underline"] = true; break;
                }
            }
            if (style.Count > 0) rules[$"{type}:{Language}"] = style;
        }
        return (types, rules);
    }

    // A value with each var(--x) replaced by app.css's value of --x.
    private static string Resolve(string value, Dictionary<string, string> properties)
    {
        while (Var().Match(value) is { Success: true } var)
        {
            value = properties.TryGetValue(var.Groups[1].Value, out string? next)
                ? next
                : throw new InvalidOperationException($"app.css has no {var.Groups[1].Value}.");
        }
        return value;
    }
}
