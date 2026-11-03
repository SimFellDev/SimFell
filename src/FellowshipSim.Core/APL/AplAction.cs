using System.Text.Json.Serialization;
using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

public class AplAction
{
    public string AbilityName { get; init; } = string.Empty;
    public IAplExpression? Condition { get; init; }
    /// <summary>Target selection of the cast; null keeps the hero's current target.</summary>
    public AplTargetIf? TargetIf { get; init; }
    public string RawLine { get; init; } = string.Empty;
    /// <summary>The line's number in its action-list text, from 1; 0 for an action not read from a text.</summary>
    public int LineNumber { get; internal set; }
    /// <summary>Set for <c>run_action_list</c> and <c>call_action_list</c>, which run <see cref="ListName"/>.</summary>
    public AplListAction? ListAction { get; init; }
    public string? ListName { get; init; }
    /// <summary>Set for <c>variable</c>, which sets a variable and casts nothing.</summary>
    public AplVariable? Variable { get; init; }

    /// <summary><c>use_off_gcd=1</c>: a candidate while the global cooldown or the reaction after it runs (<see cref="AplDecisionHold.GlobalCooldown"/>).</summary>
    public bool UseOffGcd { get; init; }

    /// <summary><c>use_while_casting=1</c>: a candidate while a cast or a channel runs (<see cref="AplDecisionHold.Cast"/>).</summary>
    public bool UseWhileCasting { get; init; }

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
        return ability == null || ability.CanCast(ctx);
    }

    public Ability? ResolveAbility(Hero hero) => AplNames.FindAbility(hero, AbilityName);

    /// <summary>
    /// Starts the action's ability. Returns false if nothing was cast, as for an action naming no ability of the hero. The
    /// cast sets the hasted global cooldown (<see cref="SimulationContext.BeginCast"/>).
    /// </summary>
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
        return ability != null && ctx.BeginCast(hero, ability, target).Succeeded;
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

    public override string ToString() => Variable != null ? $"{AplVariable.ActionName},{Variable}" + (Condition != null ? $",if={Condition}" : "") :
        AbilityName + (ListName != null ? $",{AplActionLine.ListNameKey}={ListName}" : "") + (UseOffGcd ? $",{AplActionLine.UseOffGcdKey}=1" : "") + (UseWhileCasting ? $",{AplActionLine.UseWhileCastingKey}=1" : "")
        + (TargetIf != null ? $",target_if={TargetIf}" : "") + (Condition != null ? $",if={Condition}" : "");
}

/// <summary>How a <c>variable</c> action changes its variable (<c>op=</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AplVariableOp>))]
public enum AplVariableOp
{
    /// <summary><c>op=set</c> (the default): the variable becomes the value.</summary>
    Set,
    /// <summary><c>op=add</c>: the value is added.</summary>
    Add,
    /// <summary><c>op=sub</c>: the value is taken away.</summary>
    Sub,
    /// <summary><c>op=reset</c>: the variable goes back to its default.</summary>
    Reset,
}

/// <summary>
/// A <c>variable,name=&lt;name&gt;,value=&lt;expr&gt;,op=&lt;op&gt;,default=&lt;number&gt;</c> action. <c>variable.&lt;name&gt;</c>
/// reads the variable; each fight starts it at <see cref="Default"/>.
/// </summary>
public sealed record AplVariable(string Name, AplVariableOp Op, IAplExpression? Value, double Default)
{
    public const string ActionName = "variable";
    public const string NameKey = "name";
    public const string ValueKey = "value";
    public const string OpKey = "op";
    public const string DefaultKey = "default";

    /// <summary>The op's meaning in the condition reference.</summary>
    public static string Description(AplVariableOp op) => op switch
    {
        AplVariableOp.Set => "The variable becomes the value. A variable action with no op uses set.",
        AplVariableOp.Add => "Adds the value to the variable.",
        AplVariableOp.Sub => "Subtracts the value from the variable.",
        AplVariableOp.Reset => "Sets the variable back to its default. This op needs no value.",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    public override string ToString() =>
        $"{NameKey}={Name}" + (Value != null ? $",{ValueKey}={Value}" : "") + $",{OpKey}={Op.ToString().ToLowerInvariant()}";
}
