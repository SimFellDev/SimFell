namespace FellowshipSim.Core.APL.Expressions;

/// <summary>
/// A debuff's remaining time over every application on every enemy in combat, written before a debuff path and a ':'
/// (<c>min:dot.engulfing_flames.remains</c>). Enemies without the debuff are left out.
/// </summary>
public enum AplEnemyAggregate
{
    /// <summary>The shortest remaining time of any application. No application anywhere reads as a very large number.</summary>
    Min,
    /// <summary>The longest remaining time of any application. No application anywhere reads as 0.</summary>
    Max,
}

public static class AplEnemyAggregates
{
    /// <summary>The property an aggregate reads.</summary>
    public const string Property = "remains";

    /// <summary>The aggregates by the prefix an action list writes before the ':' ("min").</summary>
    public static IReadOnlyDictionary<string, AplEnemyAggregate> ByPrefix { get; } =
        Enum.GetValues<AplEnemyAggregate>().ToDictionary(Prefix, a => a, StringComparer.OrdinalIgnoreCase);

    /// <summary>The prefix an action list writes before the ':' ("min").</summary>
    public static string Prefix(this AplEnemyAggregate aggregate) => aggregate.ToString().ToLowerInvariant();

    /// <summary>The aggregate's syntax and meaning in the condition reference.</summary>
    public static (string Syntax, AplDoc Doc) Doc(this AplEnemyAggregate aggregate) => aggregate switch
    {
        AplEnemyAggregate.Min => ($"min:{PropertyExpression.DotScope}.<aura>.{Property}", new("Shortest time remaining on any enemy",
            "Seconds until the first application of the debuff on any enemy in combat expires. Each application counts on its own, and enemies without the debuff are left out. A very large number if no enemy has the debuff.")),
        AplEnemyAggregate.Max => ($"max:{PropertyExpression.DotScope}.<aura>.{Property}", new("Longest time remaining on any enemy",
            "Seconds until the last application of the debuff on any enemy in combat expires. Enemies without the debuff are left out. 0 if no enemy has the debuff.")),
        _ => throw new ArgumentOutOfRangeException(nameof(aggregate), aggregate, null),
    };
}
