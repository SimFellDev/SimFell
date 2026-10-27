using System.Diagnostics;
using FellowshipSim.Service;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var app = ServiceApp.Build(args);
if (!ServiceApp.IsLocal(app.Configuration))
{
    app.Run();
    return;
}

// The one-person app: open the browser on it, and run until this console window closes (or Ctrl+C).
await app.StartAsync();
string url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
Console.WriteLine($"SimFell {(ServiceApp.AppVersion is { } version ? $"{version.ToString(3)} " : "")}is running at {url}");
Console.WriteLine("Close this window to stop it.");
// "--NoBrowser=true" leaves the browser closed.
if (!app.Configuration.GetValue<bool>("NoBrowser"))
{
    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception e)
    {
        Console.WriteLine($"Open {url} in your browser ({e.Message}).");
    }
}
await app.WaitForShutdownAsync();
