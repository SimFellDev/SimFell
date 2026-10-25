using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;
using FellowshipSim.UI.Apl;
using FellowshipSim.UI.Pages;
using FellowshipSim.UI.Replay;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FellowshipSim.UI;

public static class SimFellUIServiceCollectionExtensions
{
    /// <summary>
    /// The app services the pages inject: the data folder at <paramref name="dataDirectory"/> (<see cref="ReplayLibrary"/>),
    /// <see cref="LogsSettingsStore"/>, <see cref="ActionListStore"/>, the <see cref="SimJobQueue"/> with
    /// <paramref name="jobs"/>, <see cref="ReviewJobs"/>, <see cref="ReportLookup"/>, <see cref="SimResultLibrary"/>,
    /// <see cref="VisitorReviews"/>, a <see cref="DiskBlobStore"/> in the data folder when the host registered no
    /// <see cref="IBlobStore"/>, and for one circuit or window the <see cref="VisitorJobs"/> and each page's state. The host
    /// registers the <see cref="Engine.ISimEngine"/>, the <see cref="IReviewPages"/>, the <see cref="Visitor"/> and its
    /// shell with <see cref="AddSimFellWebShell"/> or <see cref="AddSimFellDesktopShell"/>.
    /// </summary>
    public static IServiceCollection AddSimFellUI(this IServiceCollection services, string dataDirectory, SimJobQueueOptions? jobs = null)
    {
        var library = new ReplayLibrary(dataDirectory);
        services.AddHttpClient(LogsSettingsStore.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(2));
        services.AddSingleton(library);
        services.TryAddSingleton<IBlobStore>(new DiskBlobStore(library.DataDirectory));
        services.AddSingleton<LogsSettingsStore>();
        services.AddSingleton<ActionListStore>();
        services.AddSingleton(new SimJobQueue(jobs));
        services.AddSingleton<ReviewJobs>();
        services.AddSingleton<ReportLookup>();
        services.AddSingleton<CharacterImport>();
        services.AddSingleton<ProfileStore>();
        services.AddSingleton<SimResultLibrary>();
        services.AddSingleton<VisitorReviews>();
        services.AddScoped<VisitorJobs>();
        services.AddScoped<FollowedReviews>();
        services.AddScoped<SimPageState>();
        services.AddScoped<ImportPageState>();
        services.AddScoped<LogReplayState>();
        services.AddScoped<ActionListsState>();
        return services;
    }

    /// <summary>The web app's views: <see cref="Routes"/> as the root, each page and result at its own address.</summary>
    public static IServiceCollection AddSimFellWebShell(this IServiceCollection services) =>
        services.AddScoped<IAppViews, WebViews>();

    /// <summary>
    /// The desktop app's views: <see cref="DesktopShell"/> as the root, each page and result in a tab. Its user is
    /// <see cref="Visitor.DataFolderOwner"/>.
    /// </summary>
    public static IServiceCollection AddSimFellDesktopShell(this IServiceCollection services) =>
        services.AddSingleton(Visitor.DataFolderOwner)
            .AddScoped<DesktopTabs>().AddScoped<IAppViews>(s => s.GetRequiredService<DesktopTabs>());
}
