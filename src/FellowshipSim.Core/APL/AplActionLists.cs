namespace FellowshipSim.Core.APL;

/// <summary>Action that runs the actions of another action list.</summary>
public enum AplListAction
{
    /// <summary><c>run_action_list</c>: the named list runs, and the actions after this one do not.</summary>
    Run,
    /// <summary><c>call_action_list</c>: the named list runs, then the next action when nothing in it was cast.</summary>
    Call,
}

public static class AplListActions
{
    public const string RunName = "run_action_list";
    public const string CallName = "call_action_list";

    public static string Name(this AplListAction action) => action == AplListAction.Run ? RunName : CallName;

    /// <summary>The list action's meaning in the condition reference.</summary>
    public static string Description(this AplListAction action) => action == AplListAction.Run
        ? "Runs the named list. The actions after this action do not run."
        : "Runs the named list. If the list casts nothing, the next action runs.";

    /// <summary>The list action written <paramref name="name"/>, or null for an ability.</summary>
    public static AplListAction? Parse(string name) => name switch
    {
        RunName => AplListAction.Run,
        CallName => AplListAction.Call,
        _ => null
    };
}

/// <summary>
/// The action lists of one script by name. Each decision starts at <see cref="AplActionLine.DefaultList"/>, and
/// <c>run_action_list</c> and <c>call_action_list</c> run another list.
/// </summary>
public sealed class AplActionLists(IReadOnlyDictionary<string, List<AplAction>> lists)
{
    public IReadOnlyDictionary<string, List<AplAction>> Lists { get; } = lists;

    /// <summary>The actions of <see cref="AplActionLine.DefaultList"/>.</summary>
    public IReadOnlyList<AplAction> Actions => Lists.GetValueOrDefault(AplActionLine.DefaultList) ?? [];

    /// <summary>The lists bound to <paramref name="hero"/> (<see cref="AplProgram.Bind"/>).</summary>
    public AplProgram Bind(Hero hero) => AplProgram.Bind(this, hero);

    // Each hero's program is bound once.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Hero, AplProgram> _bound = new();

    /// <summary>Casts the first ready action, starting at <see cref="AplActionLine.DefaultList"/>. False when nothing was cast.</summary>
    public bool TryAct(Hero hero, SimulationContext ctx) => Program(hero).TryAct(ctx);

    /// <summary>The time of the next decision after one that cast nothing (<see cref="AplProgram.NextDecision"/>).</summary>
    public SimTime NextDecision(Hero hero, SimulationContext ctx) => Program(hero).NextDecision(ctx);

    private AplProgram Program(Hero hero) => _bound.TryGetValue(hero, out var program) ? program : _bound.GetValue(hero, Bind);
}
