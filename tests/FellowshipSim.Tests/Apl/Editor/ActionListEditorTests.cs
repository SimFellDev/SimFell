using FellowshipSim.Core.APL;
using FellowshipSim.Engine;
using FellowshipSim.UI.Apl;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The editor's changes to its lists: adding, moving, duplicating, renaming and removing.</summary>
public class ActionListEditorTests
{
    private readonly AplVocabulary _v = EditorVocabulary.Create();

    private ActionListEditor Loaded(params AplDocumentAction[] actions)
    {
        var editor = new ActionListEditor(new LocalSimEngine());
        editor.Load(_v, actions);
        return editor;
    }

    [Fact]
    public void Lists_from_the_text_keep_the_actions_as_they_were_at_the_start_and_end_of_each_list()
    {
        var editor = Loaded(new("frost_bolt"), new("ice_comet", "worb >= 3"), new("frost_bolt", Comment: "filler"));
        var before = editor.Lists[0].Actions;
        editor.Toggle(before[2].Key);
        int changes = 0;
        editor.Changed += () => changes++;

        editor.Replace([new("frost_bolt"), new("ice_comet", "worb >= 4"), new("frost_bolt", Comment: "filler")]);
        var after = editor.Lists[0].Actions;
        Assert.Same(before[0], after[0]);
        Assert.NotEqual(before[1].Key, after[1].Key);
        Assert.Equal("worb >= 4", after[1].Condition);
        Assert.Same(before[2], after[2]);
        Assert.Contains(before[2].Key, editor.Open);
        Assert.Equal(1, changes);

        // A text that changes no action leaves the lists as they are.
        var lists = editor.Lists;
        editor.Replace([new("frost_bolt"), new("ice_comet", "worb >= 4"), new("frost_bolt", Comment: "filler")]);
        Assert.Same(lists, editor.Lists);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_new_action_is_open_and_its_removal_closes_it()
    {
        var editor = Loaded();
        int changes = 0;
        editor.Changed += () => changes++;

        editor.AddAction(_v.DefaultList);
        int key = editor.Lists[0].Actions.Single().Key;
        Assert.Contains(key, editor.Open);

        editor.RemoveAction(_v.DefaultList, 0);
        Assert.Empty(editor.Lists[0].Actions);
        Assert.DoesNotContain(key, editor.Open);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void The_lists_have_edits_after_a_change_not_after_opening_an_action()
    {
        var state = new ActionListsState(new LocalSimEngine());
        state.Editor.Load(_v, [new AplDocumentAction("a")]);
        Assert.True(state.HasEdits);
        state.MarkClean();
        Assert.False(state.HasEdits);

        state.Editor.Toggle(state.Editor.Lists[0].Actions[0].Key);
        state.Editor.SetStatus(MessageState.Ok("Saved."));
        Assert.False(state.HasEdits);

        state.Editor.AddAction(_v.DefaultList);
        Assert.True(state.HasEdits);
    }

    [Fact]
    public void An_action_moves_within_its_list()
    {
        var editor = Loaded(new AplDocumentAction("a"), new AplDocumentAction("b"), new AplDocumentAction("c"));

        editor.MoveAction(_v.DefaultList, 0, 2);
        Assert.Equal(["b", "c", "a"], editor.Lists[0].Actions.Select(a => a.Ability));

        editor.MoveAction(_v.DefaultList, 0, -1);
        editor.MoveAction(_v.DefaultList, 2, 3);
        Assert.Equal(["b", "c", "a"], editor.Lists[0].Actions.Select(a => a.Ability));
    }

    [Fact]
    public void A_duplicate_is_inserted_after_its_action_with_a_new_key()
    {
        var editor = Loaded(new AplDocumentAction("a"), new AplDocumentAction("b", Condition: "fury > 50"), new AplDocumentAction("c"));
        var action = editor.Lists[0].Actions[1];
        editor.Toggle(action.Key);

        editor.DuplicateAction(_v.DefaultList, 1);

        var actions = editor.Lists[0].Actions;
        Assert.Equal(["a", "b", "b", "c"], actions.Select(a => a.Ability));
        Assert.Equal(action with { Key = actions[2].Key }, actions[2]);
        Assert.NotEqual(action.Key, actions[2].Key);
        Assert.Contains(actions[2].Key, editor.Open);
    }

    [Fact]
    public void A_rename_renames_the_actions_naming_the_list()
    {
        var editor = Loaded(new AplDocumentAction(_v.ListActions[0], ActionList: "aoe"), new AplDocumentAction("a", List: "aoe"));

        Assert.False(editor.RenameList("aoe", "and"));
        Assert.Equal(MessageState.Error("`and` is an operator."), editor.Status);

        Assert.True(editor.RenameList("aoe", "cleave"));
        Assert.Equal(MessageState.None, editor.Status);
        Assert.Equal([_v.DefaultList, "cleave"], editor.Lists.Select(l => l.Name));
        Assert.Equal("cleave", editor.Lists[0].Actions[0].ActionList);
    }

    [Fact]
    public void A_list_an_action_names_is_not_removed()
    {
        var editor = Loaded(new AplDocumentAction(_v.ListActions[1], ActionList: "aoe"), new AplDocumentAction("a", List: "aoe"));

        editor.RemoveList("aoe");
        Assert.Equal(MessageState.Error("Default list has an action naming aoe. Change that action first."), editor.Status);
        Assert.Equal(2, editor.Lists.Count);

        editor.ChangeAction(editor.Lists[0].Actions[0].Key, a => a with { Ability = "frost_bolt", ActionList = null });
        editor.RemoveList("aoe");
        Assert.Single(editor.Lists);
    }

    [Fact]
    public void A_new_list_has_one_open_action_and_expand_all_opens_every_action()
    {
        var editor = Loaded(new AplDocumentAction("a"), new AplDocumentAction("b"));

        Assert.False(editor.AddList(_v.DefaultList));
        Assert.True(editor.AddList("aoe"));
        Assert.Contains(editor.Lists[1].Actions.Single().Key, editor.Open);

        editor.ToggleAll(_v.DefaultList, true);
        Assert.All(editor.Lists[0].Actions, a => Assert.Contains(a.Key, editor.Open));
        editor.ToggleAll(_v.DefaultList, false);
        Assert.All(editor.Lists[0].Actions, a => Assert.DoesNotContain(a.Key, editor.Open));
        editor.Toggle(editor.Lists[0].Actions[0].Key);
        Assert.Contains(editor.Lists[0].Actions[0].Key, editor.Open);
    }
}
