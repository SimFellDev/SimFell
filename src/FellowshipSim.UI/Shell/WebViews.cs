using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;
using Microsoft.AspNetCore.Components;

namespace FellowshipSim.UI.Shell;

/// <summary>The web app's views: each one is a page at its own address.</summary>
public sealed class WebViews(NavigationManager navigation) : IAppViews
{
    public void ShowPage(AppPage page) => navigation.NavigateTo(page.Route);

    public void ShowJob(SimJob job) => navigation.NavigateTo(Pages.Job.Href(job.Id));

    public void ShowSavedSim(SavedSim sim) => navigation.NavigateTo(Pages.Job.Href(sim.Id));

    public void ShowReview(string htmlFile, string title) => navigation.NavigateTo(Pages.LogReplay.Href(htmlFile));
}
