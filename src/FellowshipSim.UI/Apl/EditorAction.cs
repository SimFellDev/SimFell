using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// One action in the editor: the fields of an <see cref="AplDocumentAction"/> without its list, and a key that stays the
/// same while the action moves or changes.
/// </summary>
public sealed record EditorAction(
    int Key,
    string Ability = "",
    string? Condition = null,
    AplTargetSelection? TargetSelection = null,
    string? TargetExpression = null,
    string? Comment = null,
    string? Arguments = null,
    string? ActionList = null)
{
    private static int _nextKey;

    /// <summary>An action with a new key and no ability.</summary>
    public static EditorAction New() => new(Interlocked.Increment(ref _nextKey));

    /// <summary>An action with a new key and the fields of <paramref name="action"/>.</summary>
    public static EditorAction From(AplDocumentAction action) => new(
        Interlocked.Increment(ref _nextKey), action.Ability, action.Condition, action.TargetSelection, action.TargetExpression,
        action.Comment, action.Arguments, action.ActionList);

    /// <summary>An action with a new key and the fields of this action.</summary>
    public EditorAction Duplicate() => this with { Key = Interlocked.Increment(ref _nextKey) };
}
