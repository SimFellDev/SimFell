using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace FellowshipSim.UI.Shell;

/// <summary>
/// A page of the app's menu: its title and component. The web app routes to it at the component's <c>@page</c> address;
/// the desktop app shows it as a tab.
/// </summary>
public sealed record AppPage(string Title, Type Component)
{
    public static readonly AppPage Sim = new("Sim", typeof(Pages.Sim));
    public static readonly AppPage LogReplay = new("Log Replay", typeof(Pages.LogReplay));
    public static readonly AppPage AplEditor = new("APL Editor", typeof(Pages.ActionLists));
    public static readonly AppPage Setup = new("Settings", typeof(Pages.Setup));

    /// <summary>
    /// The menu, in order. The first page opens when the app starts. <see cref="Setup"/> is out of it for now: only
    /// developers set an API client (the repository's git-ignored settings.json).
    /// </summary>
    public static IReadOnlyList<AppPage> All { get; } = [Sim, LogReplay, AplEditor];

    /// <summary>The sections of the web app's top bar.</summary>
    public static IReadOnlyList<AppPage> Sections { get; } = [Sim, LogReplay, AplEditor];

    /// <summary>Address of the page relative to the base address, from the component's first <c>@page</c> template.</summary>
    public string Route { get; } = Component.GetCustomAttribute<RouteAttribute>()!.Template.TrimStart('/');
}
