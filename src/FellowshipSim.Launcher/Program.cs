using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FellowshipSim.Launcher;

/// <summary>
/// Starts SimFell in the Runtime folder beside this exe (the web app as one person's, <c>--Local=true</c>) with this exe's
/// arguments, and exits. SimFell runs in its own console window and opens the browser on itself; closing that window stops it.
/// </summary>
internal static class Program
{
    /// <summary>The folder beside SimFell.exe that holds the published app.</summary>
    private const string RuntimeFolder = "Runtime";

    /// <summary>The app's exe in <see cref="RuntimeFolder"/>.</summary>
    private const string AppExe = "FellowshipSim.Service.exe";

    /// <summary>The app as one person's, on this machine (FellowshipSim.Service's ServiceApp.LocalKey).</summary>
    private const string AppArguments = "--Local=true";

    [STAThread]
    private static int Main(string[] args)
    {
        string runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RuntimeFolder);
        string app = Path.Combine(runtime, AppExe);
        if (!File.Exists(app))
        {
            MessageBox.Show($"SimFell could not find {Path.Combine(RuntimeFolder, AppExe)} beside SimFell.exe. Extract the whole SimFell folder from the zip and run SimFell.exe from it.",
                "SimFell", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        try
        {
            Process.Start(new ProcessStartInfo(app, string.Join(" ", new[] { AppArguments }.Concat(args.Select(Quote))))
            {
                WorkingDirectory = runtime,
                UseShellExecute = false,
            });
            return 0;
        }
        catch (Exception e)
        {
            MessageBox.Show($"SimFell could not start: {e.Message}", "SimFell", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string Quote(string arg) => arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0 ? arg : "\"" + arg.Replace("\"", "\\\"") + "\"";
}
