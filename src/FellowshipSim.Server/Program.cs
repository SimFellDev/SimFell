using FellowshipSim.Logs;
using FellowshipSim.Server.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Downloaded fights, replay scripts, review pages and local settings live in the data folder: "DataDirectory" in
// configuration, otherwise "data" next to the solution file.
string dataDirectory = builder.Configuration["DataDirectory"] ?? DefaultDataDirectory(builder.Environment.ContentRootPath);

builder.Services.AddControllers();
builder.Services.AddHttpClient("fellowshiplogs", c => c.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddSingleton(new ReplayLibrary(dataDirectory));
builder.Services.AddSingleton<LogsSettingsStore>();
builder.Services.AddSingleton<ReviewJobs>();
builder.Services.AddSingleton<ActionListStore>();

var app = builder.Build();

var library = app.Services.GetRequiredService<ReplayLibrary>();
// The pages change with every build and review run, so the browser re-checks them each time (no stale page after an update).
static void NoCache(StaticFileResponseContext ctx) => ctx.Context.Response.Headers.CacheControl = "no-cache";
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = NoCache });
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(library.ReviewsDirectory),
    RequestPath = "/reviews",
    OnPrepareResponse = NoCache,
});
app.MapControllers();

app.Logger.LogInformation("Data folder: {Data}", library.DataDirectory);
app.Run();

static string DefaultDataDirectory(string contentRoot)
{
    for (var dir = new DirectoryInfo(contentRoot); dir != null; dir = dir.Parent)
    {
        if (dir.GetFiles("FellowshipSim.slnx").Length > 0) return Path.Combine(dir.FullName, "data");
    }
    return Path.Combine(contentRoot, "data");
}
