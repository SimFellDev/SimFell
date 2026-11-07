using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;
using FellowshipSim.Engine;
using FellowshipSim.Engine.Services;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The check and completion list of a .simc file: the engine's check of its action list (<see cref="AplListCheck"/>) and its
/// profile (<see cref="SimProfile.Problems"/>), the tokens of both, <see cref="ListEdits"/> on its action lines and
/// <see cref="SimcProfileEdits"/> on its profile lines.
/// </summary>
public sealed class SimcLanguage
{
    private readonly Vocabularies _vocabularies = new();

    /// <summary>The check of the file. A file with no hero the engine has has no action list check.</summary>
    public SimcCheck? Check(string? path, string text) => Safely(() =>
    {
        var file = SimcFile.Of(path, text);
        var actionList = file.Hero is { } hero ? _vocabularies.For(hero).CheckList(file.ActionList) : null;
        var tokens = SimcProfileTokens.Of(file, text, comments: actionList == null);
        if (actionList != null) tokens.AddRange(actionList.Tokens);
        return new SimcCheck(tokens, actionList, SimProfile.Problems(file.Profile));
    });

    /// <summary>The completion list at <paramref name="offset"/>. <paramref name="always"/> is true for a list the user asked for.</summary>
    public CompletionList? Complete(string? path, string text, int offset, bool always) => Safely(() =>
    {
        var file = SimcFile.Of(path, text);
        bool onProfileLine = file.ProfileLines.Contains(new LineMap(text).PositionOf(offset).Line);
        var actions = file.Hero is { } hero && !onProfileLine ? ListEdits.Open(_vocabularies.For(hero), file.ActionList, offset, always) : null;
        return SimcProfileEdits.Open(file, text, offset, always, actions);
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
