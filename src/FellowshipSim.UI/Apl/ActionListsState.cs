using FellowshipSim.Engine;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// The action-list editor's lists and fields, kept for the circuit or window, so unsaved edits last while another page
/// shows.
/// </summary>
public sealed class ActionListsState(ISimEngine engine)
{
    public ActionListEditor Editor { get; } = new(engine);

    /// <summary>True once the heroes, the first hero's vocabulary and the saved lists have loaded.</summary>
    public bool Loaded { get; set; }

    public IReadOnlyList<string> Heroes { get; set; } = [];

    public IReadOnlyList<ActionListEntry> Saved { get; set; } = [];

    /// <summary>The saved list picked, as hero/name; empty for a new list.</summary>
    public string SelectedSaved { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>
    /// The name and text of the list last opened from text (an imported profile's or a sim's), null before one: the
    /// embedded picker keeps it as an option after a saved list is picked.
    /// </summary>
    public (string Name, string Text)? TextList { get; set; }

    public string NewList { get; set; } = "";

    /// <summary>The action-list text.</summary>
    public string Text { get; set; } = "";

    /// <summary>True when the Action list text tab shows, as it does when a saved list does not parse.</summary>
    public bool TextOpen { get; set; }

    public MessageState TextStatus { get; set; } = MessageState.None;

    /// <summary>The lists as last loaded or saved: every edit gives the editor new lists.</summary>
    public IReadOnlyList<ActionList>? CleanLists { get; set; }

    /// <summary>True when the text has an edit that is not in the editor's lists yet: it waits for its delay or does not parse.</summary>
    public bool TextEdited { get; set; }

    /// <summary>True when the editor's lists or the text have changed since they were last loaded or saved.</summary>
    public bool HasEdits => !ReferenceEquals(Editor.Lists, CleanLists) || TextEdited;

    /// <summary>Marks the editor's lists as loaded or saved.</summary>
    public void MarkClean() => CleanLists = Editor.Lists;
}
