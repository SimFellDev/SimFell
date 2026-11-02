using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;
using FellowshipSim.Engine.Services;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The action-list editor's check and completion list for a .simc file: the engine's check of the file's action list
/// (<see cref="AplListCheck"/>), and <see cref="ListEdits"/> on its action lines.
/// </summary>
public sealed class SimcLanguage
{
    private readonly Vocabularies _vocabularies = new();

    /// <summary>The check of the file's action list, or null for a file with no hero the engine has.</summary>
    public AplExpressionCheck? Check(string? path, string text) => Safely(() =>
        SimcFile.Of(path, text) is { Hero: { } hero } file ? _vocabularies.For(hero).CheckList(file.ActionList) : null);

    /// <summary>
    /// The completion list at <paramref name="offset"/>, or null on a profile line or in a file with no hero the engine has.
    /// <paramref name="always"/> is true for a list the user asked for.
    /// </summary>
    public CompletionList? Complete(string? path, string text, int offset, bool always) => Safely(() =>
    {
        var file = SimcFile.Of(path, text);
        if (file.Hero is not { } hero || file.ProfileLines.Contains(new LineMap(text).PositionOf(offset).Line)) return null;
        return ListEdits.Open(_vocabularies.For(hero), file.ActionList, offset, always);
    });

    // A check that throws is no check, as in the app (ExpressionChecks): the file keeps no colours and no problems.
    private static T? Safely<T>(Func<T?> work) where T : class
    {
        try
        {
            return work();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return null;
        }
    }

    /// <summary>The protocol's token type of each <see cref="AplTokenKind"/>, in the enum's order: <c>apl</c> and the kind's name.</summary>
    public static IReadOnlyList<string> TokenTypes { get; } = Enum.GetValues<AplTokenKind>().Select(TokenType).ToList();

    public static string TokenType(AplTokenKind kind) => $"apl{kind}";

    /// <summary>The index of <paramref name="kind"/>'s type in <see cref="TokenTypes"/>.</summary>
    public static int TokenIndex(AplTokenKind kind) => Array.IndexOf(Enum.GetValues<AplTokenKind>(), kind);
}
