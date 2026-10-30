using FellowshipSim.Engine;
using FellowshipSim.UI;
using FellowshipSim.UI.Services;
using FellowshipSim.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

// The browser app: FellowshipSim.UI in the page, with every sim and replay on the visitor's own computer (the sim workers)
// and their files in the browser's storage (BrowserDataFolder). Its one visitor owns the data folder.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<BrowserDataFolder>();
builder.Services.AddSingleton<SimWorkerPool>();
builder.Services.AddSingleton<ISimEngine, WorkerSimEngine>();
builder.Services.AddSingleton<IReviewPages, BrowserReviewPages>();
builder.Services.AddSingleton<IFightReviewer, WorkerFightReviewer>();
builder.Services.AddSingleton<IDungeonImport, WorkerDungeonImport>();
builder.Services.AddSingleton<ISignInLauncher, BrowserSignInLauncher>();
builder.Services.AddSingleton(Visitor.DataFolderOwner);
builder.Services.AddSimFellUI(BrowserDataFolder.Root).AddSimFellWebShell();

var host = builder.Build();
// A sim's threads are the sim workers: one per core but the page's. A new sim takes half the cores.
var pool = host.Services.GetRequiredService<SimWorkerPool>();
SimThreads.Available = await pool.SizeAsync();
int cores;
try
{
    cores = await pool.CoresAsync();
}
catch (JSException)
{
    // A sim script that does not have it: the workers and the page's core. The app starts all the same.
    cores = SimThreads.Available + 1;
}
SimThreads.Default = Math.Min(SimThreads.Available, SimThreads.HalfOf(cores));
var data = host.Services.GetRequiredService<BrowserDataFolder>();
await data.RestoreAsync();
data.KeepSaving();
await host.RunAsync();
