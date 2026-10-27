using FellowshipSim.Engine;
using FellowshipSim.UI;
using FellowshipSim.UI.Services;
using Microsoft.Extensions.Configuration;

namespace FellowshipSim.Desktop;

public static class MauiProgram
{
    /// <summary>
    /// The app: FellowshipSim.UI in a BlazorWebView, the engine in this process (<see cref="LocalSimEngine"/>) and the data
    /// folder FellowshipSim in the user's local application data.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Configuration.AddUserSecrets(typeof(MauiProgram).Assembly);
        builder.Configuration.AddEnvironmentVariables();

        string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FellowshipSim");
        builder.Services.AddSimFellUI(dataDirectory).AddSimFellDesktopShell();
        builder.Services.AddSingleton<IDataFolder>(new DesktopDataFolder(dataDirectory));
        builder.Services.AddSingleton<ISimEngine, LocalSimEngine>();
        builder.Services.AddSingleton<ReviewPages>();
        builder.Services.AddSingleton<IReviewPages>(s => s.GetRequiredService<ReviewPages>());
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
