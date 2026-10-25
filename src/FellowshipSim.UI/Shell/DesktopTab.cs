using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.UI.Shell;

/// <summary>
/// A tab of the desktop app. <paramref name="Key"/> is unique among the tabs: the page's type, the job's ID or the
/// review page's file name. A job tab has the queue's <paramref name="Job"/>, or, after a restart, the
/// <paramref name="SavedSim"/> read back from the data folder.
/// </summary>
public sealed record DesktopTab(string Key, DesktopTabKind Kind, string Title, AppPage? Page = null, SimJob? Job = null,
    SavedSim? SavedSim = null, string? ReviewFile = null)
{
    public bool Closable => Kind != DesktopTabKind.Page;
}
