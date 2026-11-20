namespace FellowshipSim.Core.APL;

/// <summary>
/// The <c>switch_target,target_if=&lt;selection&gt;:&lt;expr&gt;</c> action: the hero's current target becomes the
/// <c>target_if</c> pick among the enemies in combat, and stays that enemy until it dies or a later <c>switch_target</c>
/// runs. The action casts nothing and takes no time, so the decision goes on at the next action. Its <c>if</c> reads the
/// enemy picked. The target stays as it is with no enemy in combat, with <c>first</c> finding no enemy, with a false
/// <c>if</c>, and with the pick already the current target. A decision while a cast or a channel runs
/// (<see cref="AplDecisionHold.Cast"/>) skips the action.
/// </summary>
public static class AplSwitchTarget
{
    public const string ActionName = "switch_target";

    /// <summary>The action as the visual editor and the condition reference name it.</summary>
    public const string Label = "Switch target";

    /// <summary>The action's meaning in the condition reference.</summary>
    public const string Description =
        "Sets the hero's current target to the enemy the target_if picks among the enemies in combat, and casts nothing. " +
        "The actions after this action run in the same decision and read the new target. " +
        "The target stays that enemy until it dies or another switch_target action runs. " +
        "In the if of the action, target.* and debuff.* refer to the enemy picked. " +
        "The action does not run while a cast or a channel runs.";
}
