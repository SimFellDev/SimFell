using System.Text.Json.Serialization;
using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>Target selection of <c>target_if</c>, written before a ':' (<c>target_if=min:expr</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AplTargetSelection>))]
public enum AplTargetSelection
{
    /// <summary>The first active enemy for which the expression is true; the action is skipped when there is none. Written without a prefix or as "first:".</summary>
    First,
    /// <summary>The active enemy with the lowest value of the expression (the first of them on a tie).</summary>
    Min,
    /// <summary>The active enemy with the highest value of the expression (the first of them on a tie).</summary>
    Max,
}

/// <summary>
/// An action's <c>target_if</c>: target selection of its cast. <c>target.*</c> and <c>debuff.*</c> in <see cref="Expression"/>,
/// and in the action's <c>if</c>, read the enemy being considered.
/// </summary>
public sealed record AplTargetIf(AplTargetSelection Selection, IAplExpression Expression)
{
    /// <summary>The prefix before ':' in action lists ("first", "min", "max").</summary>
    public static string Prefix(AplTargetSelection selection) => selection.ToString().ToLowerInvariant();

    /// <summary>The selection as the visual editor names it.</summary>
    public static string Label(AplTargetSelection selection) => selection switch
    {
        AplTargetSelection.First => "First enemy where",
        AplTargetSelection.Min => "Enemy with the lowest",
        AplTargetSelection.Max => "Enemy with the highest",
        _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, null)
    };

    /// <summary>
    /// Target among <paramref name="candidates"/>, or null when <see cref="AplTargetSelection.First"/> finds
    /// none. Each candidate is the hero's current target while the expression reads it.
    /// </summary>
    public EnemyTarget? Select(Hero hero, SimulationContext ctx, IReadOnlyList<EnemyTarget> candidates)
    {
        EnemyTarget? best = null;
        double bestValue = 0;
        foreach (var enemy in candidates)
        {
            if (Selection == AplTargetSelection.First)
            {
                if (AplAction.AtTarget(ctx, enemy, () => Expression.Evaluate(hero, ctx))) return enemy;
                continue;
            }
            double value = AplAction.AtTarget(ctx, enemy, () => Expression.EvaluateNumeric(hero, ctx));
            if (best == null || (Selection == AplTargetSelection.Min ? value < bestValue : value > bestValue))
            {
                best = enemy;
                bestValue = value;
            }
        }
        return best;
    }

    public override string ToString() => $"{Prefix(Selection)}:{Expression}";
}
