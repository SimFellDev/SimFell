using System.Text.Json;
using FellowshipSim.Cli;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;

// simfell --profile <file.simc> [options]: one Monte Carlo sim in this process. The result is the only stdout output;
// progress and errors are on stderr. Exit codes: 0 done, 1 refused profile or sim, 2 usage error, 130 cancelled.

CliOptions? options;
try
{
    options = CliOptions.Parse(args);
}
catch (CliUsageException e)
{
    Console.Error.WriteLine($"simfell: {e.Message}");
    Console.Error.WriteLine("Run simfell --help for the options.");
    return 2;
}
if (options == null)
{
    Console.Out.Write(CliOptions.Usage);
    Console.Out.WriteLine();
    return 0;
}

string path = Path.GetFullPath(options.ProfilePath);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"simfell: no profile at {path}");
    return 1;
}

SimcProfile profile;
SimSettings settings;
try
{
    var sim = options.Read(File.ReadAllText(path));
    profile = sim.Profile;
    settings = sim.Settings;
}
catch (FormatException e)
{
    Console.Error.WriteLine($"simfell: {path}: {e.Message}");
    return 1;
}
if (string.IsNullOrWhiteSpace(profile.ActionList))
{
    Console.Error.WriteLine($"simfell: {path} has no actions lines, so the hero casts nothing.");
    return 1;
}

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};

var progress = new ProgressLine(Console.Error);
SimResult result;
try
{
    result = await new LocalSimEngine().SimulateAsync(profile, settings, progress, cancel.Token);
}
catch (OperationCanceledException) when (cancel.IsCancellationRequested)
{
    progress.Clear();
    Console.Error.WriteLine("simfell: the sim was cancelled.");
    return 130;
}
catch (SimEngineException e)
{
    progress.Clear();
    Console.Error.WriteLine($"simfell: {e.Message}");
    return 1;
}
progress.Clear();

Console.Out.Write(options.Format == CliOutputFormat.Json
    ? JsonSerializer.Serialize(result, JsonSerializerOptions.Web) + Environment.NewLine
    : TextSummary.Format(result));
return 0;
