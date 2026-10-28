using FellowshipSim.Engine;
using FellowshipSim.UI;
using FellowshipSim.UI.Services;
using FellowshipSim.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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
builder.Services.AddSingleton<ISignInLauncher, BrowserSignInLauncher>();
builder.Services.AddSingleton(Visitor.DataFolderOwner);
builder.Services.AddSingleton(s => new SimResultLibrary(s.GetRequiredService<IBlobStore>()) { Keep = BrowserDataFolder.KeptSims });
builder.Services.AddSimFellUI(BrowserDataFolder.Root).AddSimFellWebShell();

var host = builder.Build();
// A sim's threads are the sim workers: one per core but the page's.
SimThreads.Available = SimThreads.Default = await host.Services.GetRequiredService<SimWorkerPool>().SizeAsync();
var data = host.Services.GetRequiredService<BrowserDataFolder>();
await data.RestoreAsync();
data.KeepSaving();
await host.RunAsync();
