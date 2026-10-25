using FellowshipSim.Core.APL;
using FellowshipSim.Engine;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// The action-list editor's state: the hero's vocabulary, the lists, the open actions, the action being dragged and the
/// status line. Each change raises <see cref="Changed"/>; <see cref="ActionListsCard"/> re-renders on it.
/// </summary>
public sealed class ActionListEditor(ISimEngine engine)
{
    public const string BuilderMode = "builder";
    public const string TextMode = "text";

    public AplVocabulary? Vocabulary { get; private set; }
    public IReadOnlyList<ActionList> Lists { get; private set; } = [];
    public IReadOnlySet<int> Open => _open;
    public DragFrom? DragFrom { get; private set; }
    public MessageState Status { get; private set; } = MessageState.None;
    public ExpressionChecks Checks { get; } = new(engine);
    /// <summary>The condition editor a newly opened action shows: <see cref="BuilderMode"/> or <see cref="TextMode"/>.</summary>
    public string ConditionMode { get; set; } = BuilderMode;

    private readonly HashSet<int> _open = [];

    public event Action? Changed;

    /// <summary>Raised when a condition field's Builder or Text button sets <see cref="ConditionMode"/>.</summary>
    public event Action<string>? ConditionModePicked;

    public void PickConditionMode(string mode)
    {
        ConditionMode = mode;
        ConditionModePicked?.Invoke(mode);
    }

    private AplVocabulary V => Vocabulary ?? throw new InvalidOperationException("The editor has no vocabulary yet.");

    public void SetVocabulary(AplVocabulary vocabulary)
    {
        Vocabulary = vocabulary;
        Changed?.Invoke();
    }

    public void SetStatus(MessageState status)
    {
        Status = status;
        Changed?.Invoke();
    }

    /// <summary>Shows <paramref name="actions"/> as lists with every action closed.</summary>
    public void Load(AplVocabulary vocabulary, IReadOnlyList<AplDocumentAction> actions)
    {
        Vocabulary = vocabulary;
        Lists = ActionListModel.ListsOf(vocabulary, actions);
        _open.Clear();
        Changed?.Invoke();
    }

    public void SetDragFrom(DragFrom? from)
    {
        DragFrom = from;
        Changed?.Invoke();
    }

    private void UpdateList(string listName, Func<IReadOnlyList<EditorAction>, IReadOnlyList<EditorAction>> change) =>
        Lists = Lists.Select(l => l.Name == listName ? l with { Actions = change(l.Actions) } : l).ToList();

    public void Toggle(int key)
    {
        if (!_open.Remove(key)) _open.Add(key);
        Changed?.Invoke();
    }

    /// <summary>Shows the details of every action in the list, or hides them all.</summary>
    public void ToggleAll(string listName, bool show)
    {
        foreach (var action in Lists.First(l => l.Name == listName).Actions)
        {
            if (show) _open.Add(action.Key);
            else _open.Remove(action.Key);
        }
        Changed?.Invoke();
    }

    public void AddAction(string listName)
    {
        var action = EditorAction.New();
        UpdateList(listName, actions => [.. actions, action]);
        _open.Add(action.Key);
        Changed?.Invoke();
    }

    public void MoveAction(string listName, int from, int to)
    {
        UpdateList(listName, actions =>
        {
            if (to < 0 || to >= actions.Count || from == to) return actions;
            var next = actions.ToList();
            var moved = next[from];
            next.RemoveAt(from);
            next.Insert(to, moved);
            return next;
        });
        Changed?.Invoke();
    }

    public void RemoveAction(string listName, int index)
    {
        int key = Lists.First(l => l.Name == listName).Actions[index].Key;
        UpdateList(listName, actions => actions.Where((_, i) => i != index).ToList());
        _open.Remove(key);
        Changed?.Invoke();
    }

    public void ChangeAction(int key, Func<EditorAction, EditorAction> change)
    {
        Lists = Lists.Select(l => l with { Actions = l.Actions.Select(a => a.Key == key ? change(a) : a).ToList() }).ToList();
        Changed?.Invoke();
    }

    /// <summary>Renames the list and the actions that name it; false, with the problem in <see cref="Status"/>, when the name is refused.</summary>
    public bool RenameList(string listName, string renamed)
    {
        var v = V;
        if (ActionListModel.ListNameProblem(Lists, renamed, listName) is { } problem)
        {
            SetStatus(MessageState.Error(problem));
            return false;
        }
        Status = MessageState.None;
        Lists = Lists.Select(l => new ActionList(
            l.Name == listName ? renamed : l.Name,
            l.Actions.Select(a => ActionListModel.IsListAction(v, a) && a.ActionList == listName ? a with { ActionList = renamed } : a).ToList()))
            .ToList();
        Changed?.Invoke();
        return true;
    }

    public void RemoveList(string listName)
    {
        var v = V;
        var runBy = Lists.FirstOrDefault(l => l.Actions.Any(a => ActionListModel.IsListAction(v, a) && a.ActionList == listName));
        if (runBy != null)
        {
            SetStatus(MessageState.Error($"{ActionListModel.ListTitle(v, runBy)} has an action naming {listName}. Change that action first."));
            return;
        }
        Status = MessageState.None;
        Lists = Lists.Where(l => l.Name != listName).ToList();
        Changed?.Invoke();
    }

    /// <summary>Adds a list with one open action; false, with the problem in <see cref="Status"/>, when the name is refused.</summary>
    public bool AddList(string listName)
    {
        if (ActionListModel.ListNameProblem(Lists, listName) is { } problem)
        {
            SetStatus(MessageState.Error(problem));
            return false;
        }
        Status = MessageState.None;
        var action = EditorAction.New();
        Lists = [.. Lists, new ActionList(listName, [action])];
        _open.Add(action.Key);
        Changed?.Invoke();
        return true;
    }
}
