using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.UI.Pages;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Debugger;

/// <summary>The fight the APL Debugger has: none yet, a sim's typical fight, or an action list from the APL Editor.</summary>
public enum AplDebuggerSource { None, SimFight, ActionList }

/// <summary>
/// The APL Debugger's fight, its traced fight and the decision on show, kept for the circuit or window while another page
/// shows. A sim result opens its typical fight (<see cref="OpenSimFight"/>); the APL Editor opens its action list
/// (<see cref="OpenActionList"/>), fought with a build and a fight picked on the debugger. Only the latest traced fight
/// is kept.
/// </summary>
public sealed class AplDebuggerState
{
    public AplDebuggerSource Source { get; private set; }

    /// <summary>Raised when a sim result or the APL Editor gives the debugger a fight, so a debugger on show takes it.</summary>
    public event Action? Opened;

    /// <summary>The sim's title, for a sim's typical fight.</summary>
    public string SimTitle { get; private set; } = "";

    /// <summary>The profile of a sim's typical fight: the run's build and action list.</summary>
    public SimcProfile? SimProfile { get; private set; }

    /// <summary>The settings of a sim's typical fight: one fight, of the typical fight's seed.</summary>
    public SimSettings? SimSettings { get; private set; }

    /// <summary>The result of the sim a typical fight is of.</summary>
    public SimResult? SimResult { get; private set; }

    /// <summary>The hero of the APL Editor's action list.</summary>
    public string Hero { get; private set; } = "";

    /// <summary>The APL Editor's action list: its name, then its text as written.</summary>
    public string ListName { get; private set; } = "";

    public string ListText { get; private set; } = "";

    /// <summary>The fight of the APL Editor's action list, set on the debugger as on the Sim page's Modes tab.</summary>
    public SimPageState Fight { get; } = new();

    /// <summary>The build the APL Editor's action list is fought with, as its picker keeps it.</summary>
    public string BuildValue { get; set; } = "";

    /// <summary>The picked saved build, read when it is picked; null for the Sim page's build or a new build.</summary>
    public CharacterBuild? SavedBuild { get; set; }

    /// <summary>The visitor's saved builds of every hero.</summary>
    public IReadOnlyList<ProfileEntry> Builds { get; set; } = [];

    /// <summary>The seed of the APL Editor's fight: the first fight of a sim with this seed.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>True when the fight is to be traced as the debugger shows: a sim's typical fight, once per opening.</summary>
    public bool RunPending { get; set; }

    public MessageState Status { get; set; } = MessageState.None;

    /// <summary>
    /// Opens the typical fight of a sim titled <paramref name="title"/> of <paramref name="profile"/> with
    /// <paramref name="settings"/>, its result <paramref name="result"/>, and traces it as the debugger shows.
    /// </summary>
    public void OpenSimFight(string title, string profile, SimSettings settings, SimResult result)
    {
        Source = AplDebuggerSource.SimFight;
        SimTitle = title;
        SimProfile = FellowshipSim.Engine.SimProfile.Parse(profile).Profile;
        SimSettings = settings with { Iterations = 1, FirstSeed = settings.FirstSeed + result.TypicalFight!.Value };
        SimResult = result;
        Open(runs: true);
    }

    /// <summary>Opens <paramref name="hero"/>'s action list <paramref name="name"/> of the APL Editor, its text <paramref name="text"/>.</summary>
    public void OpenActionList(string hero, string name, string text)
    {
        if (!hero.Equals(Hero, StringComparison.OrdinalIgnoreCase))
        {
            BuildValue = "";
            SavedBuild = null;
        }
        Source = AplDebuggerSource.ActionList;
        Hero = hero;
        ListName = name;
        ListText = text;
        Open(runs: false);
    }

    private void Open(bool runs)
    {
        Trace = null;
        Status = MessageState.None;
        RunPending = runs;
        Opened?.Invoke();
    }

    /// <summary>The traced fight on show, or null before the first run.</summary>
    public AplFightTrace? Trace { get; private set; }

    /// <summary>The build's name in <see cref="Trace"/>.</summary>
    public string TraceBuild { get; private set; } = "";

    /// <summary>The index of the decision on show in <see cref="Trace"/>.</summary>
    public int Index { get; private set; }

    /// <summary>The lines whose expressions show, by line index; the cast line shows unless it is here.</summary>
    private readonly HashSet<int> _toggled = [];

    public AplDecision? Decision => Trace is { Decisions.Count: > 0 } t ? t.Decisions[Index] : null;

    public void Show(AplFightTrace trace, string build)
    {
        Trace = trace;
        TraceBuild = build;
        Index = 0;
        _toggled.Clear();
    }

    public void Go(int index)
    {
        if (Trace == null) return;
        Index = Math.Clamp(index, 0, Trace.Decisions.Count - 1);
    }

    /// <summary>Shows the last decision at or before <paramref name="seconds"/>, or the first decision when none is.</summary>
    public void GoToTime(double seconds)
    {
        if (Trace == null) return;
        int index = 0;
        for (int i = 0; i < Trace.Decisions.Count && Trace.Decisions[i].Time <= seconds; i++) index = i;
        Go(index);
    }

    /// <summary>The next decision after the one on show, in <paramref name="direction"/> (1 or -1), that cast; null for none.</summary>
    public int? NextCast(int direction)
    {
        if (Trace == null) return null;
        for (int i = Index + direction; i >= 0 && i < Trace.Decisions.Count; i += direction)
        {
            if (Trace.Decisions[i].Cast != null) return i;
        }
        return null;
    }

    /// <summary>True when line <paramref name="line"/>'s expressions show: the cast line until it is closed, any other line once opened.</summary>
    public bool IsOpen(int line) => _toggled.Contains(line) != (line == Decision?.CastLine);

    public void Toggle(int line)
    {
        if (!_toggled.Remove(line)) _toggled.Add(line);
    }
}
