using FellowshipSim.UI.Services;

namespace FellowshipSim.Desktop;

/// <summary>The desktop app's data folder, opened in File Explorer or the Finder.</summary>
public sealed class DesktopDataFolder(string path) : IDataFolder
{
    public async Task OpenAsync()
    {
        Directory.CreateDirectory(path);
#if WINDOWS
        using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { ArgumentList = { path } });
        await Task.CompletedTask;
#else
        await Launcher.Default.OpenAsync(new Uri(new DirectoryInfo(path).FullName + Path.DirectorySeparatorChar));
#endif
    }
}
