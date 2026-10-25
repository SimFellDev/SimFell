using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A kind of value a builder row reads: a scope of the vocabulary with its properties, a resource, or a fight value.</summary>
public sealed record ConditionKind(string Id, string Label, IReadOnlyList<AplName> Names, IReadOnlyList<string>? Properties = null);
