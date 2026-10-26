using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// A kind of value a builder term reads: a scope of the vocabulary with its properties, a resource, a fight value, a part
/// of the build, or a variable. <see cref="Token"/> is the token kind of its names in the text. <see cref="MatchesStart"/>
/// marks a kind whose path names any name it starts ("weapon.voidbringer").
/// </summary>
public sealed record ConditionKind(string Id, string Label, AplTokenKind Token, IReadOnlyList<AplName> Names, IReadOnlyList<string>? Properties = null,
    bool MatchesStart = false);
