namespace FellowshipSim.Engine.Replay;

/// <summary>Everything one review found about one <see cref="IssueEntity"/>.</summary>
/// <param name="Impact">The damage at stake as a fraction of the log's damage; null for an entity with no damage of its own.</param>
/// <param name="Headline">The finding's numbers in one line.</param>
/// <param name="Facts">The finding's other numbers, a line each.</param>
/// <param name="Issues">Every issue of the entity. The findings of a review hold each of its issues once.</param>
/// <param name="Examples">The first, median and worst issue of each kind of issue.</param>
/// <param name="SampleHits">A damage ID's median and worst normal and crit hits, and its first engine-only and missing hits.</param>
public sealed record Finding(
    IssueEntity Entity,
    string Name,
    FindingGroup Group,
    double? Impact,
    string Headline,
    IReadOnlyList<string> Facts,
    IReadOnlyList<DamageSplit> Splits,
    IReadOnlyList<ReplayIssue> Issues,
    IReadOnlyList<ReplayIssue> Examples)
{
    public DamageMetrics? Damage { get; init; }
    public DriftMetrics? Drift { get; init; }
    public ProcMetrics? Proc { get; init; }
    public IReadOnlyList<HitPair> SampleHits { get; init; } = [];
}
