namespace FellowshipSim.Cli;

/// <summary>A command line <see cref="CliOptions.Parse"/> refused, with the reason.</summary>
public sealed class CliUsageException(string message) : Exception(message);
