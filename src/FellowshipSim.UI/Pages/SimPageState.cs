using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.UI.Build;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Pages;

/// <summary>The Sim page's tabs.</summary>
public enum SimTab { Import, Hero, Modes, Advanced }

/// <summary>The fights a sim runs: a dungeon's pulls, or training dummies for the whole fight.</summary>
public enum SimMode { Dungeon, TrainingDummies }

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
    /// <summary>How far each fight's length varies at first: ±20%.</summary>
    public const double DefaultLengthVariancePercent = 20;

    /// <summary>How far each dungeon fight's hero share varies at first: ±20%, 30 to 45% of 37.5%.</summary>
    public const double DefaultHeroShareVariancePercent = 20;

    public SimTab Tab { get; set; } = SimTab.Hero;

    /// <summary>
    /// The build being edited. A build of another hero puts in that hero's action list: the one last picked for them,
    /// else their default list.
    /// </summary>
    public BuildEditor Editor
    {
        get => _editor;
        set
        {
            _editor = value;
            if (!value.Build.Hero.Equals(_actionListHero, StringComparison.OrdinalIgnoreCase)) SwapActionList(value.Build.Hero);
        }
    }

    private BuildEditor _editor = new(BuildEditor.NewBuild("Rime"));

    public CharacterBuild Build => Editor.Build;

    /// <summary>The saved build the editor shows, by name; empty for a build not saved yet.</summary>
    public string SavedName { get; set; } = "";

    public MessageState BuildStatus { get; set; } = MessageState.None;

    // The build's profile when it was last loaded, saved or started anew.
    private string _cleanProfile;

    public SimPageState() => _cleanProfile = new SimcProfile(Build, "").Format();

    /// <summary>True when the build has changed since it was last loaded, saved or started anew.</summary>
    public bool HasBuildEdits => new SimcProfile(Build, "").Format() != _cleanProfile;

    /// <summary>Marks the build as loaded, saved or started anew.</summary>
    public void MarkBuildClean() => _cleanProfile = new SimcProfile(Build, "").Format();

    /// <summary>Puts back the build as it was last loaded, saved or started anew.</summary>
    public void DiscardBuildEdits() => Editor = new BuildEditor(SimcProfile.Parse(_cleanProfile).Build);

    /// <summary>
    /// Edits <paramref name="build"/> from now on, without what the engine does not have, and shows the Hero tab. A
    /// warning names what the engine does not have.
    /// </summary>
    public void UseBuild(CharacterBuild build)
    {
        var removed = BuildEditor.RemoveUnknown(build);
        Editor = new BuildEditor(build);
        SavedName = "";
        MarkBuildClean();
        BuildStatus = removed.Count > 0
            ? new MessageState($"Left out (not in the engine): {string.Join(", ", removed)}.", MessageKind.Warning)
            : MessageState.None;
        Tab = SimTab.Hero;
    }

    /// <summary>A .simc profile or a build's share link pasted on the Import tab.</summary>
    public string ProfileText { get; set; } = "";

    public MessageState ImportStatus { get; set; } = MessageState.None;

    public SimMode Mode { get; set; } = SimMode.TrainingDummies;

    public int Iterations { get; set; } = 1000;

    public double DurationSeconds { get; set; } = 300;

    /// <summary>The training dummies of a training dummy fight.</summary>
    public int TrainingDummies { get; set; } = 1;

    /// <summary>The enemies of a training dummy fight; a dungeon's pulls set their own.</summary>
    public int Targets => Mode == SimMode.TrainingDummies ? TrainingDummies : 1;

    /// <summary>The dungeon of a dungeon fight.</summary>
    public DungeonProfile Dungeon { get; set; } = DefaultDungeons.Default;

    /// <summary>The pull builder's open pulls and the pull being dragged.</summary>
    public DungeonEditor DungeonEditor { get; } = new();

    /// <summary>The dungeon picked, as kept/name ("builtin/Default", "own/Quick"); empty for one not saved yet.</summary>
    public string DungeonValue { get; set; } = $"builtin/{DefaultDungeons.DefaultName}";

    // The dungeon's text when it was last loaded or saved.
    private string _cleanDungeon = DefaultDungeons.Default.Format();

    /// <summary>True when the dungeon has changed since it was last loaded or saved.</summary>
    public bool HasDungeonEdits => Dungeon.Format() != _cleanDungeon;

    /// <summary>Marks the dungeon as loaded or saved.</summary>
    public void MarkDungeonClean() => _cleanDungeon = Dungeon.Format();

    /// <summary>A fight's length before variance: the dungeon's time limit, or <see cref="DurationSeconds"/> for training dummies.</summary>
    public double FightSeconds => Mode == SimMode.Dungeon ? Dungeon.TimeLimitSeconds : DurationSeconds;

    /// <summary>True when each fight's length varies by <see cref="LengthVariancePercent"/>.</summary>
    public bool VaryLength { get; set; } = true;

    public double LengthVariancePercent { get; set; } = DefaultLengthVariancePercent;

    /// <summary>True when each dungeon fight's hero share varies by <see cref="HeroShareVariancePercent"/>.</summary>
    public bool VaryHeroShare { get; set; } = true;

    public double HeroShareVariancePercent { get; set; } = DefaultHeroShareVariancePercent;

    /// <summary>True when the training dummies are bosses: Ruby's Blessing of the Conqueror counts only then.</summary>
    public bool Boss { get; set; } = true;

    /// <summary>True when the hero starts each fight with <see cref="StartingSpirit"/> Spirit Points.</summary>
    public bool StartWithSpirit { get; set; }

    public double StartingSpirit { get; set; } = 100;

    /// <summary>
    /// The settings of <paramref name="iterations"/> fights of this fight on <paramref name="threads"/> threads, the first
    /// with seed <paramref name="firstSeed"/>.
    /// </summary>
    public SimSettings FightSettings(int iterations, int threads, int firstSeed = 1) => new(iterations, FightSeconds, Targets, firstSeed,
        DegreeOfParallelism: threads,
        LengthVariance: VaryLength ? LengthVariancePercent / 100 : 0,
        HealthFollowsTime: true,
        StartingSpirit: StartWithSpirit ? StartingSpirit : 0,
        Boss: Boss,
        Dungeon: Mode == SimMode.Dungeon ? Dungeon : null,
        HeroShareVariance: VaryHeroShare ? HeroShareVariancePercent / 100 : 0);

    public SimExtra Extra { get; set; } = SimExtra.None;

    public List<CompareRow> CompareRows { get; } = [];

    public FinderKind Finder { get; set; } = FinderKind.Aspects;

    public FinderRanks FinderRanks { get; set; } = FinderRanks.NextRank;

    /// <summary>Threads a sim runs on: at first <see cref="SimThreads.Default"/>, until the browser's stored choice is read.</summary>
    public int Threads { get; set; } = SimThreads.Default;

    /// <summary>The browser's stored choice of <see cref="Threads"/> has been read (once for the circuit or window).</summary>
    public bool ThreadsRestored { get; set; }

    /// <summary>The action list picked, as kept/hero/name; empty for one that came with an imported profile.</summary>
    public string ActionListValue { get; set; } = $"builtin/Rime/{Core.APL.DefaultActionLists.DefaultName}";

    public string ActionListName { get; set; } = Core.APL.DefaultActionLists.DefaultName;

    /// <summary>The text of the action list picked; null for the hero's built-in default list.</summary>
    public string? ActionListText { get; set; }

    // The hero whose action list is picked, and the list last picked for each other hero.
    private string _actionListHero = "Rime";
    private readonly Dictionary<string, (string Value, string Name, string? Text)> _heroActionLists = new(StringComparer.OrdinalIgnoreCase);

    private void SwapActionList(string hero)
    {
        _heroActionLists[_actionListHero] = (ActionListValue, ActionListName, ActionListText);
        (ActionListValue, ActionListName, ActionListText) = _heroActionLists.TryGetValue(hero, out var last)
            ? last
            : ($"builtin/{hero}/{Core.APL.DefaultActionLists.DefaultName}", Core.APL.DefaultActionLists.DefaultName, null);
        _actionListHero = hero;
    }

    public MessageState Status { get; set; } = MessageState.None;

    /// <summary>Raised when something outside the Sim page changes its build (a sim's run loaded), so the page shows it.</summary>
    public event Action? Changed;

    /// <summary>Tells the Sim page its build or tab changed.</summary>
    public void NotifyChanged() => Changed?.Invoke();
}
