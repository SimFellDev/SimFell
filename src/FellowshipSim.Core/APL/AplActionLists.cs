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

    /// <summary>Casts the first ready action, starting at <see cref="AplActionLine.DefaultList"/>. False when nothing was cast.</summary>
    public bool TryAct(Hero hero, SimulationContext ctx) => TryAct(hero, ctx, AplActionLine.DefaultList, []);

    // A list already running in this decision is skipped, so lists that name each other end.
    private bool TryAct(Hero hero, SimulationContext ctx, string list, HashSet<string> running)
    {
        if (!Lists.TryGetValue(list, out var actions) || !running.Add(list)) return false;
        try
        {
            foreach (var action in actions)
            {
                if (action.ListAction is { } listAction)
                {
                    if (running.Contains(action.ListName!) || !action.ConditionHolds(hero, ctx)) continue;
                    bool acted = TryAct(hero, ctx, action.ListName!, running);
                    if (acted || listAction == AplListAction.Run) return acted;
                    continue;
                }
                if (action.CanExecute(hero, ctx) && action.Execute(hero, ctx)) return true;
            }
            return false;
        }
        finally
        {
            running.Remove(list);
        }
    }
}
