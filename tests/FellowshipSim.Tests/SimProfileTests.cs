using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests;

/// <summary>A sim's whole profile (<see cref="SimProfile"/>): the character, the sim settings, a dungeon and the action list.</summary>
public class SimProfileTests
{
    private const string ActionList = """
        # The opener
        actions=frost_bolt
        actions+=/cold_snap
        # Last line
        """;

    private const string DungeonText = """
        # A dungeon's comment
        dungeon=Quick
        difficulty=10
        time_limit=900
        # A pull's comment
        pull.1=/duration=45
        # A group's comment
        pull.1+=/count=5,name=Imp,spirit_points=3,hit_points=7857
        pull.last_boss=/final=1,duration=90
        pull.last_boss+=/boss.1,name="Ancient Koros",spirit_points=45,hit_points=64029
        """;

    private static SimcProfile Character() => new(BuildEditor.NewBuild("Rime"), ActionList.ReplaceLineEndings() + Environment.NewLine);

    [Fact]
    public void Training_Dummy_Profile_Reads_Back_Its_Settings()
    {
        var settings = new SimSettings(2500, 240, TargetCount: 3, FirstSeed: 7, DegreeOfParallelism: 6, LengthVariance: 0.15,
            HealthFollowsTime: false, StartingSpirit: 80, Boss: false, ReactionDelays: false);
        string text = new SimProfile(Character(), settings).Format();

        var read = SimProfile.Parse(text);

        Assert.Equal(settings, read.Settings);
        Assert.Equal(Character().ActionList, read.Profile.ActionList);
        Assert.Equal(text, read.Format());
        Assert.DoesNotContain(SimProfile.Dungeon, read.SettingKeys);
    }

    [Fact]
    public void Dungeon_Profile_Keeps_The_Dungeons_And_The_Action_Lists_Comments()
    {
        var dungeon = DungeonProfile.Parse(DungeonText);
        var settings = new SimSettings(500, dungeon.TimeLimitSeconds, LengthVariance: 0.2, Dungeon: dungeon);
        string text = new SimProfile(Character(), settings).Format();

        var read = SimProfile.Parse(text);

        Assert.Equal(dungeon.Format(), read.Settings.Dungeon!.Format());
        Assert.Equal(900, read.Settings.DurationSeconds);
        Assert.Equal(Character().ActionList, read.Profile.ActionList);
        Assert.Equal(text, read.Format());
        Assert.Contains(SimProfile.Dungeon, read.SettingKeys);
        Assert.DoesNotContain(SimProfile.Duration, read.SettingKeys);
    }

    /// <summary>
    /// The real-PPM first proc floor is on in a dungeon and off on training dummies unless a line says otherwise, and a line set
    /// either way reads back.
    /// </summary>
    [Fact]
    public void The_First_Proc_Floor_Defaults_To_On_In_A_Dungeon_And_Off_On_Training_Dummies()
    {
        var dungeon = DungeonProfile.Parse(DungeonText);
        var dummies = new SimSettings(500, 300);
        var dungeonFights = new SimSettings(500, dungeon.TimeLimitSeconds, Dungeon: dungeon);
        var dummiesWithFloor = dummies with { RealPpmFirstProcFloor = true };

        var read = SimProfile.Parse(new SimProfile(Character(), dummiesWithFloor).Format());

        Assert.False(dummies.Fight.RealPpmFirstProcFloor);
        Assert.True(dungeonFights.Fight.RealPpmFirstProcFloor);
        Assert.DoesNotContain(SimProfile.FirstProcFloor, new SimProfile(Character(), dummies).Format());
        Assert.Equal(true, read.Settings.RealPpmFirstProcFloor);
        Assert.True(read.Settings.Fight.RealPpmFirstProcFloor);
        Assert.False((dungeonFights with { RealPpmFirstProcFloor = false }).Fight.RealPpmFirstProcFloor);
    }

    [Fact]
    public void Lines_Are_In_Any_Order()
    {
        var dungeon = DungeonProfile.Parse(DungeonText);
        var settings = new SimSettings(500, dungeon.TimeLimitSeconds, FirstSeed: 3, Boss: false, Dungeon: dungeon);
        string character = new SimcProfile(Character().Build, "").Format();
        string text = string.Join(Environment.NewLine, Character().ActionList, "boss=false", DungeonText, "iterations=500", character, "seed=3");

        var read = SimProfile.Parse(text);

        Assert.Equal(settings with { Dungeon = null }, read.Settings with { Dungeon = null });
        Assert.Equal(dungeon.Format(), read.Settings.Dungeon!.Format());
        Assert.Equal(Character().ActionList, read.Profile.ActionList);
        Assert.Equal(character, new SimcProfile(read.Profile.Build, "").Format());
    }

    [Fact]
    public void Last_Line_Of_A_Setting_Is_The_Setting()
    {
        string text = new SimProfile(Character(), new SimSettings(2500, 240, Boss: true)).Format() + "iterations=10\nboss=false\niterations=20\n";

        var read = SimProfile.Parse(text);

        Assert.Equal(20, read.Settings.Iterations);
        Assert.False(read.Settings.Boss);
    }

    [Fact]
    public void Last_Dungeon_Replaces_The_Ones_Before()
    {
        var first = DungeonProfile.Parse(DungeonText);
        string text = new SimProfile(Character(), new SimSettings(10, first.TimeLimitSeconds, Dungeon: first)).Format()
                      + "\n" + DefaultDungeons.Default.Format();

        var read = SimProfile.Parse(text);

        Assert.Equal(DefaultDungeons.Default.Format(), read.Settings.Dungeon!.Format());
        Assert.Equal(DefaultDungeons.Default.TimeLimitSeconds, read.Settings.DurationSeconds);
    }

    [Fact]
    public void Character_Profile_Has_No_Settings()
    {
        var read = SimProfile.Parse(new SimcProfile(BuildEditor.NewBuild("Rime"), "actions=frost_bolt\n").Format());

        Assert.Empty(read.SettingKeys);
        Assert.Equal(SimProfile.DefaultIterations, read.Settings.Iterations);
        Assert.Equal(SimProfile.DefaultDurationSeconds, read.Settings.DurationSeconds);
    }

    [Theory]
    [InlineData("iterations=0")]
    [InlineData("boss=maybe")]
    [InlineData("speed=2")]
    public void Bad_Setting_Names_Its_Line(string line)
    {
        string character = new SimcProfile(BuildEditor.NewBuild("Rime"), "").Format();
        int lineNumber = character.TrimEnd().Split('\n').Length + 1;

        var e = Assert.ThrowsAny<FormatException>(() => SimProfile.Parse(character + line + "\n"));

        Assert.StartsWith($"Profile line {lineNumber} ", e.Message);
    }
}
