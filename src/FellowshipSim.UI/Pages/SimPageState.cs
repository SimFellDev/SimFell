using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.UI.Build;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Pages;

/// <summary>The Sim page's tabs. Import is a dialog the Hero tab opens.</summary>
public enum SimTab { Hero, Modes, Advanced }

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
            BuildPlaced = true;
            if (!value.Build.Hero.Equals(_actionListHero, StringComparison.OrdinalIgnoreCase)) SwapActionList(value.Build.Hero);
        }
    }

    private BuildEditor _editor = new(BuildEditor.NewBuild("Rime"));

    /// <summary>
    /// True once a build replaced the first new build: a load, an import, a share link or a sim's run. The page opens the
    /// last build open only while this is false.
    /// </summary>
    public bool BuildPlaced { get; private set; }

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

    /// <summary>
    /// True after the Import dialog put in a build, until the Hero tab's editor saves it: under its own name when that is
    /// free, else under the name the save dialog asks for.
    /// </summary>
    public bool ImportedBuildToSave { get; set; }

    /// <summary>A .simc profile or a build's share link pasted in Import.</summary>
    public string ProfileText { get; set; } = "";

    /// <summary>True while the Import dialog shows.</summary>
    public bool ImportOpen { get; set; }

    /// <summary>The Hero tab's cards folded to their title line, by title; every card is open at first.</summary>
    public HashSet<string> CollapsedCards { get; } = [];

    public MessageState ImportStatus { get; set; } = MessageState.None;

    /// <summary>
    /// Edits the build of <see cref="ProfileText"/> from now on, with the profile's action list when it has one, and shows
    /// the Hero tab. Each sim setting the profile has replaces the page's. <see cref="ImportStatus"/> has the error of a
    /// profile that does not parse.
    /// </summary>
    public void ImportProfile()
    {
        try
        {
            if (BuildShareLink.TryRead(ProfileText) is { } shared)
            {
                UseBuild(shared);
                ImportStatus = MessageState.None;
                return;
            }
            var sim = SimProfile.Parse(ProfileText);
            // The build first: a build of another hero puts in that hero's list, and the profile's own list replaces it.
            UseBuild(sim.Profile.Build);
            if (sim.Profile.ActionList.Trim().Length > 0)
            {
                ActionListText = sim.Profile.ActionList;
                ActionListName = "the profile's list";
                ActionListValue = "";
            }
            UseSettings(sim);
            ImportStatus = MessageState.None;
        }
        catch (FormatException e)
        {
            ImportStatus = MessageState.Error(e.Message);
        }
    }

    /// <summary>
    /// Puts <paramref name="profile"/>, a sim's profile, in the Import dialog, and its build, action list and settings in
    /// the page, and opens the Import dialog. Run sim then runs the profile alone: no compare or finder. False, with the
    /// reason in <see cref="ImportStatus"/>, when the profile does not parse.
    /// </summary>
    public bool RunAgain(string profile)
    {
        ProfileText = profile;
        ImportProfile();
        bool loaded = ImportStatus.Kind != MessageKind.Error;
        if (loaded) Extra = SimExtra.None;
        ImportOpen = true;
        NotifyChanged();
        return loaded;
    }

    // Each setting of the profile's text in the page. A switched-off variance or starting Spirit keeps the page's amount.
    private void UseSettings(SimProfile sim)
    {
        var keys = sim.SettingKeys;
        var settings = sim.Settings;
        if (keys.Contains(SimProfile.Iterations)) Iterations = settings.Iterations;
        if (keys.Contains(SimProfile.Threads))
        {
            Threads = Math.Clamp(settings.DegreeOfParallelism!.Value, 1, SimThreads.Available);
            // The browser's stored choice no longer replaces the profile's.
            ThreadsRestored = true;
        }
        if (keys.Contains(SimProfile.Seed)) FirstSeed = settings.FirstSeed;
        if (keys.Contains(SimProfile.Dungeon))
        {
            var dungeon = settings.Dungeon!;
            string text = dungeon.Format();
            if (text != Dungeon.Format())
            {
                string? builtIn = DefaultDungeons.Names.FirstOrDefault(n => DungeonProfile.Parse(DefaultDungeons.Read(n)!).Format() == text);
                Dungeon = dungeon;
                DungeonEditor.CloseAll();
                DungeonValue = builtIn != null ? $"builtin/{builtIn}" : "";
                if (builtIn != null) MarkDungeonClean();
            }
            Mode = SimMode.Dungeon;
        }
        else if (keys.Contains(SimProfile.Duration) || keys.Contains(SimProfile.Targets))
        {
            Mode = SimMode.TrainingDummies;
        }
        if (keys.Contains(SimProfile.Duration)) DurationSeconds = settings.DurationSeconds;
        if (keys.Contains(SimProfile.Targets)) TrainingDummies = settings.TargetCount;
        if (keys.Contains(SimProfile.LengthVariance))
        {
            VaryLength = settings.LengthVariance > 0;
            if (VaryLength) LengthVariancePercent = settings.LengthVariance * 100;
        }
        if (keys.Contains(SimProfile.HeroShareVariance))
        {
            VaryHeroShare = settings.HeroShareVariance > 0;
            if (VaryHeroShare) HeroShareVariancePercent = settings.HeroShareVariance * 100;
        }
        if (keys.Contains(SimProfile.HealthFollowsTime)) HealthFollowsTime = settings.HealthFollowsTime;
        if (keys.Contains(SimProfile.Boss)) Boss = settings.Boss;
        if (keys.Contains(SimProfile.StartingSpirit))
        {
            StartWithSpirit = settings.StartingSpirit > 0;
            if (StartWithSpirit) StartingSpirit = settings.StartingSpirit;
        }
        if (keys.Contains(SimProfile.ReactionDelays)) ReactionDelays = settings.ReactionDelays;
        if (keys.Contains(SimProfile.ProcReaction)) ProcReactionMilliseconds = settings.ProcReactionSeconds * 1000;
    }

    /// <summary>
    /// The <c>.simc</c> profile a sim of the page runs: <paramref name="build"/> with the action list
    /// <paramref name="actionList"/>, and the page's settings. A sim's job is given this text alone.
    /// </summary>
    public string SimProfileText(CharacterBuild build, string actionList) =>
        new SimProfile(new SimcProfile(build, actionList), FightSettings(Iterations, Math.Clamp(Threads, 1, SimThreads.Available), FirstSeed)).Format();

    public SimMode Mode { get; set; } = SimMode.TrainingDummies;

    public int Iterations { get; set; } = SimProfile.DefaultIterations;

    /// <summary>The seed of a sim's first fight; fight i has this seed + i.</summary>
    public int FirstSeed { get; set; } = 1;

    /// <summary>True when the targets lose their health over each fight.</summary>
    public bool HealthFollowsTime { get; set; } = true;

    public double DurationSeconds { get; set; } = SimProfile.DefaultDurationSeconds;

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

    /// <summary>True when the hero takes a player's time to cast once the global cooldown, a cast or a channel lets them.</summary>
    public bool ReactionDelays { get; set; } = true;

    /// <summary>Milliseconds a proc's gain takes to reach the action lists; 0 for at once.</summary>
    public double ProcReactionMilliseconds { get; set; } = SimSettings.DefaultProcReactionSeconds * 1000;

    /// <summary>
    /// The settings of <paramref name="iterations"/> fights of this fight on <paramref name="threads"/> threads, the first
    /// with seed <paramref name="firstSeed"/>.
    /// </summary>
    public SimSettings FightSettings(int iterations, int threads, int firstSeed = 1) => new(iterations, FightSeconds, Targets, firstSeed,
        DegreeOfParallelism: threads,
        LengthVariance: VaryLength ? LengthVariancePercent / 100 : 0,
        HealthFollowsTime: HealthFollowsTime,
        StartingSpirit: StartWithSpirit ? StartingSpirit : 0,
        Boss: Boss,
        Dungeon: Mode == SimMode.Dungeon ? Dungeon : null,
        HeroShareVariance: VaryHeroShare ? HeroShareVariancePercent / 100 : 0,
        ReactionDelays: ReactionDelays,
        ProcReactionSeconds: ProcReactionMilliseconds / 1000);

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

    // Each hero's action-list editor on the Modes tab, kept while another hero's build shows.
    private readonly Dictionary<string, Apl.ActionListsState> _actionListEditors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Modes tab's action-list editor of <paramref name="hero"/>, its own for each hero.</summary>
    public Apl.ActionListsState ActionListEditor(string hero, ISimEngine engine)
    {
        if (!_actionListEditors.TryGetValue(hero, out var editor)) _actionListEditors[hero] = editor = new Apl.ActionListsState(engine);
        return editor;
    }

    /// <summary>True when the Modes tab shows the action list's editor under its picker.</summary>
    public bool ActionListEditorOpen { get; set; }

    /// <summary>Raised when something outside the Sim page changes its build (a sim's run loaded), so the page shows it.</summary>
    public event Action? Changed;

    /// <summary>Tells the Sim page its build or tab changed.</summary>
    public void NotifyChanged() => Changed?.Invoke();
}
