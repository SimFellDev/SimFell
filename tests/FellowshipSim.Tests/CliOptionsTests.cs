using FellowshipSim.Cli;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests;

public class CliOptionsTests
{
    // A character profile with no sim settings.
    private static string Character() => new SimcProfile(BuildEditor.NewBuild("Rime"), "actions=frost_bolt\n").Format();

    [Fact]
    public void Profile_Alone_Gets_The_Default_Settings_And_Text()
    {
        var options = CliOptions.Parse(["--profile", "rime.simc"])!;

        Assert.Equal("rime.simc", options.ProfilePath);
        Assert.Empty(options.Lines);
        Assert.Equal(CliOptions.DefaultSettings, options.Read(Character()).Settings);
        Assert.Equal(CliOutputFormat.Text, options.Format);
    }

    [Fact]
    public void Every_Option_Sets_Its_Setting()
    {
        var options = CliOptions.Parse(["--profile", "rime.simc", "--iterations", "200", "--duration=90.5", "--targets", "3",
            "--seed", "-7", "--threads", "2", "--format", "json"])!;

        Assert.Equal(new SimSettings(200, 90.5, TargetCount: 3, FirstSeed: -7, DegreeOfParallelism: 2), options.Read(Character()).Settings);
        Assert.Equal(CliOutputFormat.Json, options.Format);
    }

    [Fact]
    public void Options_Replace_The_Profiles_Settings()
    {
        var profileSettings = new SimSettings(750, 200, TargetCount: 2, FirstSeed: 4, LengthVariance: 0.2, Boss: false);
        string profile = new SimProfile(new SimcProfile(BuildEditor.NewBuild("Rime"), "actions=frost_bolt\n"), profileSettings).Format();
        var options = CliOptions.Parse(["--profile", "rime.simc", "--iterations", "50", "--reactions", "off"])!;

        Assert.Equal(profileSettings with { Iterations = 50, ReactionDelays = false }, options.Read(profile).Settings);
    }

    [Fact]
    public void Dungeon_Option_Replaces_The_Profiles_Dungeon()
    {
        var mine = DefaultDungeons.Default with { Name = "Mine", TimeLimitSeconds = 600 };
        string profile = new SimProfile(new SimcProfile(BuildEditor.NewBuild("Rime"), "actions=frost_bolt\n"), new SimSettings(10, 600, Dungeon: mine)).Format();
        var options = CliOptions.Parse(["--profile", "rime.simc", "--dungeon", DefaultDungeons.DefaultName])!;

        var settings = options.Read(profile).Settings;

        Assert.Equal(DefaultDungeons.Default.Format(), settings.Dungeon!.Format());
        Assert.Equal(DefaultDungeons.Default.TimeLimitSeconds, settings.DurationSeconds);
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
