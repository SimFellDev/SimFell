using FellowshipSim.Core.Import;
using FellowshipSim.UI.Build;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Pages;

/// <summary>The Sim page's tabs.</summary>
public enum SimTab { Import, Hero, Modes, Advanced }

/// <summary>The fights a sim runs: one target, or several at once.</summary>
public enum SimMode { SingleTarget, Aoe }

/// <summary>What a sim runs besides its fight: the build alone, a list of builds and action lists, or a finder's variations.</summary>
public enum SimExtra { None, Compare, Finder }

/// <summary>
/// One run of a compare: a saved build (<see cref="Saved"/>), or the editor's build when null, with the action list
/// <see cref="ListValue"/> (as the picker keeps it; empty for the hero's default list).
/// </summary>
public sealed class CompareRow
{
    public ProfileEntry? Saved { get; set; }

    public string ListValue { get; set; } = "";
}

/// <summary>The Sim page's build, fight settings and messages, kept for the circuit or window while another page shows.</summary>
public sealed class SimPageState
{
    /// <summary>The targets of an AoE sim at first.</summary>
    public const int DefaultAoeTargets = 8;

    /// <summary>How far each fight's length varies at first: ±20%.</summary>
    public const double DefaultLengthVariancePercent = 20;

    public SimTab Tab { get; set; } = SimTab.Hero;

    /// <summary>The build being edited.</summary>
    public BuildEditor Editor { get; set; } = new(BuildEditor.NewBuild("Rime"));

    public CharacterBuild Build => Editor.Build;

    /// <summary>The saved build the editor shows, by name; empty for a build not saved yet.</summary>
    public string SavedName { get; set; } = "";

    public MessageState BuildStatus { get; set; } = MessageState.None;

    /// <summary>A .simc profile pasted on the Import tab.</summary>
    public string ProfileText { get; set; } = "";

    public MessageState ImportStatus { get; set; } = MessageState.None;

    public SimMode Mode { get; set; } = SimMode.SingleTarget;

    public int Iterations { get; set; } = 1000;

    public double DurationSeconds { get; set; } = 300;

    public int AoeTargets { get; set; } = DefaultAoeTargets;

    public int Targets => Mode == SimMode.SingleTarget ? 1 : AoeTargets;

    /// <summary>True when each fight's length varies by <see cref="LengthVariancePercent"/>.</summary>
    public bool VaryLength { get; set; } = true;

    public double LengthVariancePercent { get; set; } = DefaultLengthVariancePercent;

    /// <summary>True when the hero starts each fight with <see cref="StartingSpirit"/> Spirit Points.</summary>
    public bool StartWithSpirit { get; set; }

    public double StartingSpirit { get; set; } = 100;

    public SimExtra Extra { get; set; } = SimExtra.None;

    public List<CompareRow> CompareRows { get; } = [];

    public FinderKind Finder { get; set; } = FinderKind.Aspects;

    public FinderRanks FinderRanks { get; set; } = FinderRanks.NextRank;

    /// <summary>Threads a sim runs on: at first all of the machine's logical processors but two.</summary>
    public int Threads { get; set; } = Math.Max(1, Environment.ProcessorCount - 2);

    /// <summary>The action list picked, as kept/hero/name; empty for one that came with an imported profile.</summary>
    public string ActionListValue { get; set; } = $"builtin/Rime/{Core.APL.DefaultActionLists.DefaultName}";

    public string ActionListName { get; set; } = Core.APL.DefaultActionLists.DefaultName;

    /// <summary>The text of the action list picked; null for the hero's built-in default list.</summary>
    public string? ActionListText { get; set; }

    public MessageState Status { get; set; } = MessageState.None;

    /// <summary>Raised when something outside the Sim page changes its build (a sim's run loaded), so the page shows it.</summary>
    public event Action? Changed;

    /// <summary>Tells the Sim page its build or tab changed.</summary>
    public void NotifyChanged() => Changed?.Invoke();
}
