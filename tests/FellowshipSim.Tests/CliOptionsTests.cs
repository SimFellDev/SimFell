using FellowshipSim.Cli;

namespace FellowshipSim.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Profile_Alone_Gets_The_Default_Settings_And_Text()
    {
        var options = CliOptions.Parse(["--profile", "rime.simc"])!;

        Assert.Equal("rime.simc", options.ProfilePath);
        Assert.Equal(CliOptions.DefaultSettings, options.Settings);
        Assert.Equal(CliOutputFormat.Text, options.Format);
    }

    [Fact]
    public void Every_Option_Sets_Its_Setting()
    {
        var options = CliOptions.Parse(["--profile", "rime.simc", "--iterations", "200", "--duration=90.5", "--targets", "3",
            "--seed", "-7", "--threads", "2", "--format", "json"])!;

        Assert.Equal(new Engine.SimSettings(200, 90.5, TargetCount: 3, FirstSeed: -7, DegreeOfParallelism: 2), options.Settings);
        Assert.Equal(CliOutputFormat.Json, options.Format);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Help_Is_Null(string flag)
    {
        Assert.Null(CliOptions.Parse(["--iterations", "5", flag]));
    }

    [Theory]
    [InlineData("--iterations 5")]
    [InlineData("--profile")]
    [InlineData("--profile --iterations 5")]
    [InlineData("--profile a.simc --iterations 0")]
    [InlineData("--profile a.simc --duration -1")]
    [InlineData("--profile a.simc --threads two")]
    [InlineData("--profile a.simc --format xml")]
    [InlineData("--profile a.simc --bogus 1")]
    [InlineData("a.simc")]
    public void Malformed_Command_Line_Is_A_Usage_Error(string commandLine)
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(commandLine.Split(' ')));
    }
}
