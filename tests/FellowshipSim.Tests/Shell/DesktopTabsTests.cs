using FellowshipSim.Engine;
using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shell;

namespace FellowshipSim.Tests.Shell;

/// <summary>The desktop app's tabs: a fixed tab per page, one closable tab per job or review page.</summary>
public class DesktopTabsTests
{
    [Fact]
    public void The_App_Opens_On_The_First_Page_With_A_Tab_For_Each_Page()
    {
        var tabs = new DesktopTabs();

        Assert.Equal(AppPage.All, tabs.Tabs.Select(t => t.Page));
        Assert.Equal(AppPage.All[0], tabs.Selected.Page);
        Assert.True(tabs.IsShown(tabs.Selected));
        Assert.False(tabs.IsShown(tabs.Tabs[1]));
    }

    [Fact]
    public void A_Review_Page_Shown_Twice_Has_One_Tab()
    {
        var tabs = new DesktopTabs();
        tabs.ShowReview("a.html", "Review: a");
        tabs.ShowPage(AppPage.LogReplay);
        tabs.ShowReview("a.html", "Review: a");

        Assert.Single(tabs.Tabs, t => t.Kind == DesktopTabKind.Review);
        Assert.Equal("a.html", tabs.Selected.ReviewFile);
    }

    [Fact]
    public void A_Job_Opens_In_Its_Own_Tab()
    {
        var tabs = new DesktopTabs();
        var job = new SimJobQueue().Enqueue("a", SimJobKind.Sim, "sim", _ => Task.FromResult(1));
        tabs.ShowJob(job);
        tabs.ShowJob(job);

        Assert.Single(tabs.Tabs, t => t.Kind == DesktopTabKind.Job);
        Assert.Same(job, tabs.Selected.Job);
    }

    [Fact]
    public void Closing_The_Selected_Tab_Selects_The_Next_Or_The_One_Before_The_Last()
    {
        var tabs = new DesktopTabs();
        tabs.ShowReview("a.html", "a");
        tabs.ShowReview("b.html", "b");
        tabs.ShowReview("c.html", "c");

        tabs.Select(tabs.Tabs.Single(t => t.ReviewFile == "b.html"));
        tabs.Close(tabs.Selected);
        Assert.Equal("c.html", tabs.Selected.ReviewFile);
        Assert.True(tabs.IsShown(tabs.Selected));

        tabs.Close(tabs.Selected);
        Assert.Equal("a.html", tabs.Selected.ReviewFile);
    }

    [Fact]
    public void Closing_Another_Tab_Keeps_The_Selection()
    {
        var tabs = new DesktopTabs();
        tabs.ShowReview("a.html", "a");
        tabs.ShowPage(AppPage.AplEditor);
        tabs.Close(tabs.Tabs.Single(t => t.ReviewFile == "a.html"));

        Assert.Equal(AppPage.AplEditor, tabs.Selected.Page);
        Assert.DoesNotContain(tabs.Tabs, t => t.Kind == DesktopTabKind.Review);
    }

    [Fact]
    public void Close_All_Leaves_The_Page_Tabs_And_Selects_The_First_Page()
    {
        var tabs = new DesktopTabs();
        tabs.ShowReview("a.html", "a");
        tabs.ShowReview("b.html", "b");
        tabs.CloseResults();

        Assert.Equal(AppPage.All, tabs.Tabs.Select(t => t.Page));
        Assert.Equal(AppPage.All[0], tabs.Selected.Page);
    }

    [Fact]
    public void A_Saved_Sim_Opens_In_The_Tab_Of_Its_Job()
    {
        var tabs = new DesktopTabs();
        var job = new SimJobQueue().Enqueue("a", SimJobKind.Sim, "sim", _ => Task.FromResult(1));
        tabs.ShowJob(job);
        tabs.ShowPage(AppPage.Sim);
        tabs.ShowSavedSim(new SavedSim(job.Id, "sim", "Rime", "sww", "", 1,
            new SimResult(1, 1, 1, 0, new Dictionary<string, double>(), 1, 1), DateTime.UtcNow));

        Assert.Single(tabs.Tabs, t => t.Kind == DesktopTabKind.Job);
        Assert.Same(job, tabs.Selected.Job);
    }

    [Fact]
    public void A_Page_Tab_Does_Not_Close()
    {
        var tabs = new DesktopTabs();
        tabs.Close(tabs.Tabs[0]);

        Assert.Equal(AppPage.All.Count, tabs.Tabs.Count);
    }

    [Fact]
    public void Each_Page_Route_Comes_From_Its_Page_Directive()
    {
        Assert.Equal(["", "replay", "apl"], AppPage.All.Select(p => p.Route));
    }
}
