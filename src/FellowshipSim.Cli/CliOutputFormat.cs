namespace FellowshipSim.Cli;

/// <summary>Format of the sim result on stdout.</summary>
public enum CliOutputFormat
{
    /// <summary>DPS summary and each ability's DPS, aligned.</summary>
    Text,

    /// <summary><see cref="FellowshipSim.Engine.SimResult"/> in <see cref="System.Text.Json.JsonSerializerOptions.Web"/>.</summary>
    Json,
}
