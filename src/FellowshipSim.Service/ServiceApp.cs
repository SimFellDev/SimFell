using FellowshipSim.Engine;
using FellowshipSim.Logs;
using FellowshipSim.Service.Components;
using FellowshipSim.Service.Services;
using FellowshipSim.UI;
using FellowshipSim.UI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;

namespace FellowshipSim.Service;

/// <summary>
/// FellowshipSim.Service: the FellowshipSim.UI pages (Blazor, Interactive Server), FellowshipLogs access, imports, jobs,
/// saved action lists and the review pages, each visitor's own (<see cref="VisitorIdentity"/>). The engine server (<see cref="RemoteSimEngine"/>) does every replay and sim
/// and reads every action list.
/// <para>
/// With <see cref="LocalKey"/> true the app is one person's, on their own machine, as the desktop app is: the engine runs
/// in this process, the person owns the data folder (their FellowshipLogs credentials included), which is FellowshipSim in
/// their local application data unless "DataDirectory" says otherwise, and the app listens on a free port of this machine
/// alone.
/// </para>
/// </summary>
public static class ServiceApp
{
    /// <summary>The configuration key ("--Local=true") of the one-person app.</summary>
    public const string LocalKey = "Local";

    /// <summary>The release workflow's version (assembly metadata SimFellVersion), or null for a build made elsewhere.</summary>
    public static Version? AppVersion { get; } = AppUpdates.Parse(typeof(ServiceApp).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "SimFellVersion")?.Value);

    /// <summary>True when <paramref name="configuration"/> asks for the one-person app.</summary>
    public static bool IsLocal(IConfiguration configuration) => configuration.GetValue<bool>(LocalKey);

    /// <summary>The service app, with <paramref name="configure"/> applied to its builder before it is built.</summary>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        // The application name picks the assembly whose static assets are served, also when another program hosts this app.
        // The one-person app finds its wwwroot beside its exe, wherever it was started from.
        bool localArgs = new ConfigurationBuilder().AddCommandLine(args).Build().GetValue<bool>(LocalKey);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(ServiceApp).Assembly.GetName().Name,
            ContentRootPath = localArgs ? AppContext.BaseDirectory : null,
        });
        // A developer's FellowshipLogs API client: the repository's settings.json, copied beside the app by its build.
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "settings.json"), optional: true);
        configure?.Invoke(builder);
        bool local = IsLocal(builder.Configuration);

        // Downloaded fights, replay scripts, review pages and local settings live in the data folder: "DataDirectory" in
        // configuration, otherwise the desktop app's folder for the one-person app, otherwise "data" next to the solution file.
        string dataDirectory = builder.Configuration["DataDirectory"]
            ?? (local ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FellowshipSim")
                : DefaultDataDirectory(builder.Environment.ContentRootPath));
        // The one-person app takes any free port of this machine; the browser it opens finds it.
        if (local && string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://127.0.0.1:0");
        // The one-person app's console shows where it runs, then warnings and errors alone: no request-by-request log, and
        // not the first run's note that the cookie keys are stored unencrypted (one person's machine, their own cookie).
        if (local)
        {
            builder.Logging.AddFilter((category, level) => level >= LogLevel.Warning
                && (level >= LogLevel.Error || category?.StartsWith("Microsoft.AspNetCore.DataProtection", StringComparison.Ordinal) != true));
        }

        builder.AddServiceDefaults();
        // A pasted profile or action list reaches the circuit as one message; SignalR's default limit is 32 KB.
        builder.Services.AddRazorComponents().AddInteractiveServerComponents()
            .AddHubOptions(o => o.MaximumReceiveMessageSize = 1024 * 1024);
        if (local) builder.Services.AddSingleton<ISimEngine, LocalSimEngine>();
        else builder.Services.AddHttpClient<ISimEngine, RemoteSimEngine>(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<IReviewPages, ReviewPages>();
        builder.Services.AddSimFellUI(dataDirectory).AddSimFellWebShell();

        // Each browser is a visitor with its own jobs, sims, reviews and action lists. The cookie keys are in the data
        // folder, so a visitor keeps their ID when the app restarts.
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(VisitorIdentity.ConfigureCookie);
        if (local)
        {
            builder.Services.AddSingleton(Visitor.DataFolderOwner);
            builder.Services.AddSingleton<IDataFolder>(new FileManagerDataFolder(dataDirectory));
            // Signing in to FellowshipLogs opens the browser; the way back comes to this computer.
            builder.Services.AddSingleton<ISignInLauncher>(new LoopbackSignInLauncher(url =>
            {
                using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                return Task.CompletedTask;
            }));
            // The packaged app tells when a newer build is released.
            builder.Services.AddHttpClient(nameof(AppUpdates), c => c.Timeout = TimeSpan.FromSeconds(10));
            // "--PretendLatestVersion=0.2.0" acts as if that version were released, to see the banner without a release.
            var pretend = AppUpdates.Parse(builder.Configuration["PretendLatestVersion"]) is { } v
                ? new AppRelease(v, AppUpdates.TagOf(v), AppUpdates.LatestReleasePage) : null;
            builder.Services.AddSingleton(s => new AppUpdates(s.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AppUpdates)), AppVersion, pretend));
        }
        else builder.Services.AddScoped(s => VisitorIdentity.Of(s.GetRequiredService<AuthenticationStateProvider>()));

        var app = builder.Build();

        var library = app.Services.GetRequiredService<ReplayLibrary>();
        app.MapDefaultEndpoints();
        app.UseAuthentication();
        app.Use(VisitorIdentity.IssueAsync);
        if (local) app.MapGet("/reviews/{file}", OwnReviewFile);
        else app.MapGet("/reviews/{file}", ReviewFileAsync);
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(Routes).Assembly);

        app.Logger.LogInformation("Data folder: {Data}", library.DataDirectory);
        return app;
    }

    /// <summary>
    /// A file of a review the visitor ran, from the reviews folder; another visitor's review is not found. A review page
    /// changes with each review run, so the browser re-checks it each time.
    /// </summary>
    private static async Task<IResult> ReviewFileAsync(string file, HttpContext context, ReplayLibrary library, VisitorReviews reviews)
    {
        string name = Path.GetFileName(file);
        string reviewId = name.Split('.')[0];
        string path = Path.Combine(library.ReviewsDirectory, name);
        if (VisitorIdentity.IdOf(context.User) is not { } id || !await reviews.HasAsync(new Visitor(id, OwnsDataFolder: false), reviewId, context.RequestAborted)
            || !File.Exists(path))
        {
            return Results.NotFound();
        }
        if (!ContentTypes.TryGetContentType(name, out string? contentType)) contentType = "application/octet-stream";
        context.Response.Headers.CacheControl = "no-cache";
        return Results.File(path, contentType);
    }

    /// <summary>A file of any review in the reviews folder: in the one-person app every review is the person's.</summary>
    private static IResult OwnReviewFile(string file, HttpContext context, ReplayLibrary library)
    {
        string name = Path.GetFileName(file);
        string path = Path.Combine(library.ReviewsDirectory, name);
        if (!File.Exists(path)) return Results.NotFound();
        if (!ContentTypes.TryGetContentType(name, out string? contentType)) contentType = "application/octet-stream";
        context.Response.Headers.CacheControl = "no-cache";
        return Results.File(path, contentType);
    }

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private static string DefaultDataDirectory(string contentRoot)
    {
        for (var dir = new DirectoryInfo(contentRoot); dir != null; dir = dir.Parent)
        {
            if (dir.GetFiles("FellowshipSim.slnx").Length > 0) return Path.Combine(dir.FullName, "data");
        }
        return Path.Combine(contentRoot, "data");
    }
}
