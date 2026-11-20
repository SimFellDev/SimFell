using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>
/// A <c>switch_target</c> action bound to its hero (<see cref="AplSwitchTarget"/>). Running it sets the hero's current
/// target to its <c>target_if</c> pick; it casts nothing.
/// </summary>
public sealed class BoundAplSwitchTarget : BoundAplStep
{
    private readonly BoundTargetIf _targetIf;

    internal BoundAplSwitchTarget(BoundTargetIf targetIf, BoundExpression? condition) : base(condition) => _targetIf = targetIf;

    /// <summary>The <c>target_if</c> selection.</summary>
    internal AplTargetSelection TargetSelection => _targetIf.Selection;

    /// <summary>The <c>target_if</c> pick among the enemies in combat: null with no enemy in combat, or when <c>first</c> finds none.</summary>
    internal EnemyTarget? Pick(SimulationContext ctx)
    {
        var active = ctx.ActiveEnemies;
        return active.Count == 0 ? null : _targetIf.Select(ctx, active);
    }

    /// <summary>
    /// Sets the hero's current target to the pick when the condition holds at the pick. True when the target changed:
    /// false with no pick, with the pick already the current target, and with a false condition.
    /// </summary>
    public bool Run(SimulationContext ctx)
    {
        if (Pick(ctx) is not { } pick || pick == ctx.Player.CurrentTarget) return false;
        if (Condition != null && !BoundTargetIf.EvaluateAt(ctx, pick, Condition)) return false;
        ctx.Player.CurrentTarget = pick;
        return true;
    }

    /// <summary>
    /// The earliest time after now at which the step's pick or its condition at the pick can change with no event before
    /// it.
    /// </summary>
    internal SimTime NextChange(SimulationContext ctx)
    {
        var next = SimTime.Infinite;
        var active = ctx.ActiveEnemies;
        if (active.Count > 0) next = _targetIf.NextChange(ctx, active);
        if (Condition != null) next = Crossing.Earlier(next, BoundTargetIf.ChangeAt(ctx, Pick(ctx), Condition));
        return next;
    }

    public override string ToString() => AplSwitchTarget.ActionName;
}
