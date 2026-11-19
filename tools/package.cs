// Packages SimFell to share, as the release workflow does, in bin/package:
//
//     SimFell/
//       SimFell.exe       Windows: the launcher (src/FellowshipSim.Launcher)
//       SimFell.command   macOS: a start script to double-click (it opens in Terminal)
//       simfell.sh        Linux: a start script
//       LICENSE.txt       the repository's LICENSE
//       Runtime/          the web app with the engine in-process, self-contained (.NET inside, nothing to install)
//     SimFell-<rid>.zip
//
// Each starts Runtime/FellowshipSim.Service --Local=true: the web app as one person's, in a console window that opens the
// browser; closing it stops SimFell.
//
//     dotnet run tools/package.cs -- [--rid win-x64|osx-arm64|osx-x64|linux-x64] [--version 0.1.0]
//
// The default is win-x64 with no version (a build with no version shows no out-of-date banner). The Windows launcher
// builds on Windows; package macOS and Linux on macOS or Linux, so the zip keeps the files' run permission.
using System.Diagnostics;
using System.IO.Compression;

string rid = "win-x64";
string? version = null;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--rid": rid = args[++i]; break;
        case "--version": version = args[++i]; break;
        default: throw new ArgumentException($"Unknown option {args[i]}. Options: --rid <runtime>, --version <major.minor.patch>.");
    }
}
bool windows = rid.StartsWith("win", StringComparison.Ordinal);

string root = FindRoot();
string package = Path.Combine(root, "bin", "package");
string app = Path.Combine(package, "SimFell");
string runtime = Path.Combine(app, "Runtime");
string zip = Path.Combine(package, $"SimFell-{rid}.zip");

if (Directory.Exists(app)) Directory.Delete(app, recursive: true);
Directory.CreateDirectory(app);

var publish = new List<string> { "publish", "src/FellowshipSim.Service", "-c", "Release", "-r", rid, "--self-contained", "true",
    "-p:SatelliteResourceLanguages=en", "-o", runtime };
if (version != null) publish.Add($"-p:SimFellVersion={version}");
Run([.. publish]);

if (windows)
{
    string launcher = Path.Combine(package, "launcher");
    Run("build", "src/FellowshipSim.Launcher", "-c", "Release", "-o", launcher);
    File.Copy(Path.Combine(launcher, "SimFell.exe"), Path.Combine(app, "SimFell.exe"));
    Directory.Delete(launcher, recursive: true);
}
else
{
    // The start script runs the app from its own folder; the window it runs in is the console that stops it.
    string script = "#!/bin/sh\n# Starts SimFell: it opens in your browser; close this window to stop it.\n" +
                    "cd \"$(dirname \"$0\")/Runtime\" && exec ./FellowshipSim.Service --Local=true \"$@\"\n";
    string name = rid.StartsWith("osx", StringComparison.Ordinal) ? "SimFell.command" : "simfell.sh";
    File.WriteAllText(Path.Combine(app, name), script.Replace("\r\n", "\n"));
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(Path.Combine(app, name), (UnixFileMode)0b111_101_101);
        File.SetUnixFileMode(Path.Combine(runtime, "FellowshipSim.Service"), (UnixFileMode)0b111_101_101);
    }
}

// The GNU Affero General Public License's text is in every copy of the app.
File.Copy(Path.Combine(root, "LICENSE"), Path.Combine(app, "LICENSE.txt"));

// Only the app: no debug symbols.
foreach (string pdb in Directory.EnumerateFiles(runtime, "*.pdb")) File.Delete(pdb);

if (File.Exists(zip)) File.Delete(zip);
ZipFile.CreateFromDirectory(app, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
Console.WriteLine($"{app}{Environment.NewLine}{zip} ({new FileInfo(zip).Length / (1024 * 1024)} MB)");
return 0;

void Run(params string[] args)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root };
    foreach (string arg in args) start.ArgumentList.Add(arg);
    // Every core: MSBuild builds the projects side by side.
    start.ArgumentList.Add($"-maxcpucount:{Environment.ProcessorCount}");
    using var process = Process.Start(start)!;
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException($"dotnet {string.Join(' ', args)} failed ({process.ExitCode}).");
}

static string FindRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "FellowshipSim.slnx"))) return dir.FullName;
    }
    throw new InvalidOperationException("Run this from inside the SimFell checkout.");
}
