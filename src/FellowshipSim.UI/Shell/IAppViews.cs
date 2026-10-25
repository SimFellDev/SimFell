using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.UI.Shell;

/// <summary>
/// Shows a page, a job or a review page. The web app goes to its address; the desktop app selects or opens its tab.
/// Every link inside the app goes through it, since the desktop app has no router.
/// </summary>
public interface IAppViews
{
    void ShowPage(AppPage page);

    /// <summary>The job's progress, then its result.</summary>
    void ShowJob(SimJob job);

    /// <summary>A finished sim read back from the data folder.</summary>
    void ShowSavedSim(SavedSim sim);

    /// <summary>The review page <paramref name="htmlFile"/> in the reviews folder, titled <paramref name="title"/>.</summary>
    void ShowReview(string htmlFile, string title);
}
