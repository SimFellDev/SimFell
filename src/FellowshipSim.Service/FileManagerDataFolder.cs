using System.Diagnostics;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Service;

/// <summary>The one-person app's data folder, opened in the file manager of the computer it runs on.</summary>
public sealed class FileManagerDataFolder(string path) : IDataFolder
{
    public Task OpenAsync()
    {
        Directory.CreateDirectory(path);
        string fileManager = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        using var _ = Process.Start(new ProcessStartInfo(fileManager) { ArgumentList = { path }, UseShellExecute = false });
        return Task.CompletedTask;
    }
}
