using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

public class AplAction
{
    public string AbilityName { get; init; } = string.Empty;
    public IAplExpression? Condition { get; init; }
    /// <summary>Target selection of the cast; null keeps the hero's current target.</summary>
    public AplTargetIf? TargetIf { get; init; }
    public string RawLine { get; init; } = string.Empty;
    /// <summary>Set for <c>run_action_list</c> and <c>call_action_list</c>, which run <see cref="ListName"/>.</summary>
    public AplListAction? ListAction { get; init; }
    public string? ListName { get; init; }

    /// <summary>True when the action has no condition or its condition is true at the current target.</summary>
    public bool ConditionHolds(Hero hero, SimulationContext ctx) => Condition == null || Condition.Evaluate(hero, ctx);

    public bool CanExecute(Hero hero, SimulationContext ctx)
    {
        if (!TrySelectTarget(hero, ctx, out var target))
        {
            return false;
        }

        if (Condition != null && !AtTarget(ctx, target, () => Condition.Evaluate(hero, ctx)))
        {
            return false;
        }

        var ability = ResolveAbility(hero);
        return ability == null || ability.CanCast(ctx, out _);
    }

    public Ability? ResolveAbility(Hero hero) => AplNames.FindAbility(hero, AbilityName);

    /// <summary>Starts the action's ability and sets the global cooldown. Returns false if nothing was cast.</summary>
    public bool Execute(Hero hero, SimulationContext ctx)
    {
        if (!TrySelectTarget(hero, ctx, out var target))
        {
            return false;
        }
        if (target == null)
        {
            target = ctx.AddEnemy(1, 1, "Target Dummy");
            ctx.Player.CurrentTarget = target;
        }

        var ability = ResolveAbility(hero);
        if (ability == null)
        {
            hero.GlobalCooldownEndTime = ctx.CurrentTime + SimTime.FromSeconds(1.0);
            return false;
        }

        var attempt = ctx.BeginCast(hero, ability, target);
        if (!attempt.Succeeded) return false;

        if (!ability.IgnoresGcd)
        {
            var gcd = attempt.CastTime == SimTime.Zero ? SimTime.FromSeconds(1.0) : attempt.CastTime;
            hero.GlobalCooldownEndTime = ctx.CurrentTime + gcd;
        }
        return true;
    }

    /// <summary>
    /// Target of the cast: the <see cref="TargetIf"/> pick among the active enemies, otherwise the current target (null
    /// when there are no enemies). False when <see cref="TargetIf"/> finds no enemy.
    /// </summary>
    private bool TrySelectTarget(Hero hero, SimulationContext ctx, out EnemyTarget? target)
    {
        target = ctx.Player.CurrentTarget ?? ctx.ActiveEnemies.FirstOrDefault();
        if (TargetIf == null || ctx.ActiveEnemies.Count == 0) return true;
        target = TargetIf.Select(hero, ctx, ctx.ActiveEnemies);
        return target != null;
    }

    /// <summary>Runs <paramref name="read"/> with <paramref name="target"/> as the hero's current target, then restores it.</summary>
    internal static T AtTarget<T>(SimulationContext ctx, EnemyTarget? target, Func<T> read)
    {
        var current = ctx.Player.CurrentTarget;
        if (target == null || target == current) return read();
        ctx.Player.CurrentTarget = target;
        try
        {
            return read();
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
    }

    public override string ToString() =>
        AbilityName + (ListName != null ? $",{AplActionLine.ListNameKey}={ListName}" : "") + (TargetIf != null ? $",target_if={TargetIf}" : "") + (Condition != null ? $",if={Condition}" : "");
}
