using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.UI.Shell;

/// <summary>
/// The desktop app's tabs: one per menu page, then a closable tab for each job and review page opened. Every tab stays
/// rendered once shown, so its state lasts while another tab is selected.
/// </summary>
public sealed class DesktopTabs : IAppViews
{
    private readonly List<DesktopTab> _tabs = AppPage.All
        .Select(p => new DesktopTab(p.Component.FullName!, DesktopTabKind.Page, p.Title, Page: p))
        .ToList();

    private readonly HashSet<string> _shown = [];

    public DesktopTabs()
    {
        Selected = _tabs[0];
        _shown.Add(Selected.Key);
    }

    /// <summary>Raised on the UI thread after any change to the tabs or the selection.</summary>
    public event Action? Changed;

    public IReadOnlyList<DesktopTab> Tabs => _tabs;

    public DesktopTab Selected { get; private set; }

    /// <summary>True for a tab selected at least once: its content is rendered.</summary>
    public bool IsShown(DesktopTab tab) => _shown.Contains(tab.Key);

    public void Select(DesktopTab tab)
    {
        Selected = tab;
        _shown.Add(tab.Key);
        Changed?.Invoke();
    }

    /// <summary>
    /// Closes a job or review tab. A selected tab passes the selection to the tab after it, or to the one before it when
    /// it is the last.
    /// </summary>
    public void Close(DesktopTab tab)
    {
        int index = _tabs.IndexOf(tab);
        if (!tab.Closable || index < 0) return;
        _tabs.RemoveAt(index);
        _shown.Remove(tab.Key);
        if (Selected == tab)
        {
            Selected = _tabs[Math.Min(index, _tabs.Count - 1)];
            _shown.Add(Selected.Key);
        }
        Changed?.Invoke();
    }

    public void ShowPage(AppPage page) => Select(_tabs.First(t => t.Page == page));

    public void ShowDebugger() => Open(new DesktopTab(DebuggerKey, DesktopTabKind.Debugger, AppPage.AplDebugger.Title));

    // The APL Debugger has one tab.
    private const string DebuggerKey = "debugger";

    public void ShowJob(SimJob job) => Open(new DesktopTab(JobKey(job.Id), DesktopTabKind.Job, job.Title, Job: job));

    public void ShowSavedSim(SavedSim sim) => Open(new DesktopTab(JobKey(sim.Id), DesktopTabKind.Job, sim.Title, SavedSim: sim));

    public void ShowReview(string htmlFile, string title) =>
        Open(new DesktopTab($"review:{htmlFile}", DesktopTabKind.Review, title, ReviewFile: htmlFile));

    /// <summary>Closes every job and review tab; a closed selected tab passes the selection to the first page.</summary>
    public void CloseResults()
    {
        _tabs.RemoveAll(t => t.Closable);
        _shown.RemoveWhere(key => _tabs.All(t => t.Key != key));
        if (!_tabs.Contains(Selected)) Selected = _tabs[0];
        Changed?.Invoke();
    }

    // A sim's live job and its saved result share a tab.
    private static string JobKey(string id) => $"job:{id}";

    private void Open(DesktopTab tab)
    {
        var open = _tabs.FirstOrDefault(t => t.Key == tab.Key);
        if (open == null)
        {
            open = tab;
            _tabs.Add(tab);
        }
        Select(open);
    }
}
