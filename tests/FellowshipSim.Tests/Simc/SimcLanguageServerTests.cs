using System.Text.Json.Nodes;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.LanguageServer;

namespace FellowshipSim.Tests.Simc;

/// <summary>The .simc language server: its hero, its action list, its tokens, diagnostics and completion list.</summary>
public class SimcLanguageServerTests
{
    private const string Profile = """
        rime="sww"
        primary_stat=161
        talents=avalanche/coalescing_frost
        # the list
        actions+=/frost_bolt,if=buff.
        actions+=/not_an_ability
        """;

    private static readonly SimcLanguage Language = new();

    [Fact]
    public void A_Profile_Has_Its_Hero_Lines_Hero()
    {
        Assert.Equal("Rime", SimcFile.Of(null, Profile).Hero);
    }

    [Fact]
    public void An_Action_List_Has_The_Hero_Of_Its_Folder()
    {
        string path = Path.Combine("src", "FellowshipSim.Core", "Heroes", "Gunde", "ActionLists", "Default.simc");
        Assert.Equal("Gunde", SimcFile.Of(Path.GetFullPath(path), "actions+=/x").Hero);
    }

    [Fact]
    public void A_File_With_No_Hero_Has_No_Action_List_Check()
    {
        Assert.Null(SimcFile.Of(null, "actions+=/x").Hero);
        var check = Language.Check(null, "actions+=/x")!;
        Assert.Null(check.ActionList);
        Assert.Empty(check.Tokens);
        Assert.Empty(check.Problems);
    }

    [Fact]
    public void The_Hero_Line_Can_Follow_Settings_And_A_Dungeon()
    {
        Assert.Equal("Rime", SimcFile.Of(null, "iterations=100\ndungeon=Default\nrime=\"x\"\nactions+=/frost_bolt").Hero);
    }

    [Fact]
    public void A_Bare_Action_With_No_Hero_Line_Is_An_Action_Line()
    {
        string path = Path.GetFullPath(Path.Combine("src", "Heroes", "Rime", "ActionLists", "Default.simc"));
        var file = SimcFile.Of(path, "frost_bolt,if=buff.chill.up");
        Assert.Empty(file.ProfileLines);
        Assert.Contains("frost_bolt", file.ActionList);
    }

    public static TheoryData<string> ShippedFiles()
    {
        var files = new TheoryData<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(TestPaths.RepositoryRoot, "src", "FellowshipSim.Core"), "*.simc", SearchOption.AllDirectories)) files.Add(file);
        files.Add(Path.Combine(TestPaths.RepositoryRoot, "benchmarks", "FellowshipSim.Benchmarks", "Fixtures", "rime.simc"));
        return files;
    }

    [Theory]
    [MemberData(nameof(ShippedFiles))]
    public void A_Shipped_File_Has_No_Error(string path)
    {
        string text = File.ReadAllText(path);
        var errors = SimcLanguageServer.Diagnostics(text, Language.Check(path, text)).Where(d => (int)d!["severity"]! == 1).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(d => (string)d!["message"]!)));
    }

    [Fact]
    public void A_Formatted_Sim_Profile_Has_No_Problem()
    {
        var build = new CharacterBuild { Hero = "Rime", Name = "x" };
        var settings = new SimSettings(100, 300, 1, Dungeon: DefaultDungeons.Default);
        Assert.Empty(SimProfile.Problems(new SimProfile(new SimcProfile(build, ""), settings).Format()));
    }

    [Theory]
    [InlineData("rime=\"x\"\nnot_a_key=1", 2, "unknown key 'not_a_key'")]
    [InlineData("rime=\"x\"\ntalents=not_a_talent", 2, "no talent 'not_a_talent'")]
    [InlineData("rime=\"x\"\nprimary_stat=many", 2, "'many' is not a number")]
    [InlineData("rime=\"x\"\niterations=0", 2, "'0' is not a whole number of at least 1")]
    [InlineData("iterations=0", 1, "'0' is not a whole number of at least 1")]
    [InlineData("boss=maybe", 1, "'maybe' is not true or false")]
    [InlineData("primary_stat=1", 1, SimcProfile.NoHeroProblem)]
    public void A_Bad_Line_Has_Its_Problem(string text, int line, string problem)
    {
        Assert.Contains(new SimProfileProblem(line, problem), SimProfile.Problems(text));
    }

    [Fact]
    public void Each_Bad_Line_Has_Its_Own_Problem()
    {
        var problems = SimProfile.Problems("rime=\"x\"\nnot_a_key=1\nprimary_stat=many\nseed=x\ndungeon=D\npull.1=/duration=x");
        Assert.Equal([2, 3, 6, 4], problems.Select(p => p.Line));
    }

    [Fact]
    public void A_Dungeon_Alone_Reads_And_Its_Problems_Are_On_Its_Lines()
    {
        string dungeon = DefaultDungeons.Read(DefaultDungeons.DefaultName)!;
        Assert.Empty(Language.Check(null, dungeon)!.Problems);

        const string text = "dungeon=D\npull.1=/duration=10,pull_with=2\npull.1+=/count=1,name=Imp,hit_points=1,spirit_points=1";
        var diagnostic = SimcLanguageServer.Diagnostics(text, Language.Check(null, text)).Single()!;
        Assert.Contains("no pull 2", (string)diagnostic["message"]!);
        Assert.Equal(1, (int)diagnostic["range"]!["start"]!["line"]!);
    }

    [Fact]
    public void A_Problem_Is_On_The_Name_It_Quotes()
    {
        const string text = "rime=\"x\"\ntalents=avalanche/not_a_talent";
        var range = SimcLanguageServer.Diagnostics(text, Language.Check(null, text)).Last()!["range"]!;
        Assert.Equal("talents=avalanche/".Length, (int)range["start"]!["character"]!);
        Assert.Equal("talents=avalanche/not_a_talent".Length, (int)range["end"]!["character"]!);
    }

    [Fact]
    public void Profile_Lines_Have_Tokens()
    {
        const string text = "# a dungeon\ndungeon=\"Cithrel's Fall\"\npull.1=/count=4,name=Imp,after_deaths=50%\nboss=true";
        var tokens = Language.Check(null, text)!.Tokens;
        string[] words(AplTokenKind kind) => tokens.Where(t => t.Kind == kind).Select(t => text.Substring(t.Start, t.Length)).ToArray();
        Assert.Equal(["# a dungeon"], words(AplTokenKind.Comment));
        Assert.Equal(["pull.1"], words(AplTokenKind.List));
        Assert.Equal(["dungeon", "count", "name", "after_deaths", "boss", "true"], words(AplTokenKind.Keyword));
        Assert.Equal(["4", "50"], words(AplTokenKind.Number));
        Assert.Equal(["\"Cithrel's Fall\"", "Imp"], words(AplTokenKind.Named));
    }

    [Fact]
    public void A_Source_Link_Is_One_Token()
    {
        const string text = "rime=\"x\"\nsource=https://example.com/a?b=1";
        var token = Language.Check(null, text)!.Tokens[^1];
        Assert.Equal("https://example.com/a?b=1", text.Substring(token.Start, token.Length));
    }

    // The items at the end of the text's '|'.
    private static IEnumerable<string> Items(string text, bool always = true)
    {
        int at = text.IndexOf('|');
        var list = Language.Complete(null, text.Remove(at, 1), at, always);
        return list?.Items.Select(i => i.Text) ?? [];
    }

    private static Completion Pick(string text, string item)
    {
        int at = text.IndexOf('|');
        return Language.Complete(null, text.Remove(at, 1), at, true)!.Items.Single(i => i.Text == item);
    }

    private const string Hero = "rime=\"x\"\n";

    [Fact]
    public void A_Line_Start_Lists_The_Keys_The_File_Does_Not_Have()
    {
        var keys = Items(Hero + "primary_stat=1\niterations=1\n|").ToList();
        Assert.DoesNotContain("primary_stat", keys);
        Assert.DoesNotContain("iterations", keys);
        Assert.DoesNotContain("frost_bolt", keys);
        Assert.Contains("actions", keys);
        Assert.Contains("dungeon", keys);
        Assert.Contains("pull", keys);
        Assert.Contains(SimProfile.Seed, Items(Hero + "se|"));
        Assert.Contains("talents", Items(Hero + "tal|"));
        Assert.Equal("talents=", Pick(Hero + "tal|", "talents").Written);
        Assert.True(Pick(Hero + "tal|", "talents").OpensNext);
    }

    [Fact]
    public void The_Affixes_Key_Lists_The_Affixes_Without_Those_Listed()
    {
        Assert.Contains("affixes", Items(Hero + "aff|"));
        Assert.Equal(["ultimatum", "anomalous_orbs", "storm_shield"], Items(Hero + "affixes=|"));
        Assert.Equal(["ultimatum", "storm_shield"], Items(Hero + "affixes=anomalous_orbs,|"));
        Assert.Contains("anomalous_orbs", Items(Hero + "affixes=anom|"));
    }

    [Fact]
    public void A_File_With_No_Hero_Line_Lists_The_Heroes()
    {
        Assert.Contains("rime", Items("ri|"));
        Assert.Equal("rime=\"", Pick("ri|", "rime").Written);
    }

    [Fact]
    public void Lists_Have_Their_Names_Without_Those_Listed()
    {
        var talents = Items(Hero + "talents=|").ToList();
        Assert.Contains("avalanche", talents);
        Assert.DoesNotContain("avalanche", Items(Hero + "talents=avalanche/|"));
        Assert.Contains("sapphire", Items(Hero + "gems=sapph|"));
        Assert.Equal("sapphire:", Pick(Hero + "gems=sapph|", "sapphire").Written);
        Assert.Empty(Items(Hero + "gems=sapphire:|"));
        Assert.NotEmpty(Items(Hero + "gem_sockets=|"));
        Assert.NotEmpty(Items(Hero + "stat_enchants=|"));
    }

    [Theory]
    [InlineData("head")]
    [InlineData("ring1")]
    [InlineData("weapon")]
    [InlineData("relic1")]
    [InlineData("legendary")]
    public void A_Gear_Line_Picks_An_Item_That_Reads(string key)
    {
        string text = $"{Hero}{key}=|";
        var item = Language.Complete(null, text.Remove(text.IndexOf('|'), 1), text.Length - 1, true)!.Items[0];
        Assert.Empty(SimProfile.Problems($"{Hero}{key}={item.Written}"));
    }

    [Fact]
    public void A_Gear_Line_Has_Its_Options_After_A_Comma()
    {
        Assert.Equal(["id", "tempers"], Items(Hero + "ring1=x,|"));
    }

    [Fact]
    public void A_Setting_Has_Its_Values()
    {
        Assert.Equal(["true", "false"], Items("boss=|"));
        Assert.Empty(Items("iterations=|"));
        Assert.Empty(Items(Hero + "primary_stat=|"));
    }

    [Fact]
    public void A_Pull_Line_Has_Its_Options_And_The_Other_Pulls()
    {
        const string pulls = "dungeon=D\npull.first=/duration=10\n";
        Assert.Contains("first", Items(pulls + "pull.|"));
        Assert.Equal("first+=/", Pick(pulls + "pull.|", "first").Written);
        Assert.Contains("count", Items(pulls + "pull.next=/|"));
        Assert.Contains("boss.1", Items(pulls + "pull.next=/|"));
        Assert.DoesNotContain("count", Items(pulls + "pull.next=/boss.1,|"));
        Assert.DoesNotContain("duration", Items(pulls + "pull.next=/duration=1,|"));
        Assert.Equal(["first"], Items(pulls + "pull.next=/duration=1,chain_with=|"));
        Assert.Empty(Items(pulls + "pull.first+=/duration=1,chain_with=|"));
    }

    [Fact]
    public void A_Name_Has_The_Creatures_Quoted()
    {
        var creature = GameCreatures.All.First(c => c.Name.Contains(' '));
        string part = creature.Name.Split(' ')[0].ToLowerInvariant();
        string text = $"pull.1=/count=1,name=\"{part}|";
        int at = text.IndexOf('|');
        var list = Language.Complete(null, text.Remove(at, 1), at, true)!;
        Assert.Equal($"\"{creature.Name}\"", list.Items.Single(i => i.Text == creature.Name).Written);
        Assert.Equal("pull.1=/count=1,name=".Length, list.From);
    }

    [Fact]
    public void The_Language_Offers_The_Abilitys_Off_Gcd_Arguments()
    {
        var keys = Items(Hero + "actions+=/frost_bolt,|").ToList();
        Assert.Contains(AplActionLine.UseOffGcdKey, keys);
        Assert.Contains(AplActionLine.UseWhileCastingKey, keys);
    }

    [Fact]
    public void A_Switch_Target_Line_Has_Its_Tokens_Its_Problem_And_Its_Completions()
    {
        const string gunde = "gunde=\"x\"\n";
        string text = gunde + "actions+=/switch_target,target_if=target.is_boss\nactions+=/switch_target";
        var check = Language.Check(null, text)!;
        var keywords = check.Tokens.Where(t => t.Kind == AplTokenKind.Keyword).Select(t => text.Substring(t.Start, t.Length)).ToList();
        Assert.Equal(2, keywords.Count(k => k == AplSwitchTarget.ActionName));
        Assert.Contains(AplActionLine.TargetIfKey, keywords);

        // The line without target_if is the file's one problem.
        var diagnostic = SimcLanguageServer.Diagnostics(text, check).Single()!;
        Assert.Contains("`switch_target` needs `target_if=<expression>`", (string)diagnostic["message"]!);
        Assert.Equal(2, (int)diagnostic["range"]!["start"]!["line"]!);

        Assert.Contains(AplSwitchTarget.ActionName, Items(gunde + "actions+=/sw|"));
        Assert.Equal([AplActionLine.ConditionKey, AplActionLine.TargetIfKey], Items(gunde + "actions+=/switch_target,|"));
        Assert.Contains("max", Items(gunde + "actions+=/switch_target,target_if=ma|"));
    }

    [Fact]
    public void Every_Profile_Key_Reads()
    {
        foreach (string key in SimcProfile.Keys)
        {
            Assert.DoesNotContain(SimProfile.Problems($"{Hero}{key}=x"), p => p.Problem.StartsWith("unknown key", StringComparison.Ordinal));
        }
        string profile = new SimProfile(new SimcProfile(new CharacterBuild { Hero = "Rime", Name = "x" }, ""), new SimSettings(1, 1, 1)).Format();
        foreach (string line in profile.Split('\n').Skip(1).Where(l => l.Trim().Length > 0))
        {
            string key = SimProfile.KeyOf(line.Trim());
            Assert.True(SimcProfile.Keys.Contains(key) || SimProfile.SettingNames.Contains(key), key);
        }
    }

    [Fact]
    public void Every_Pull_Key_Reads()
    {
        foreach (string key in DungeonProfile.GroupKeys.Concat(DungeonProfile.PullKeys))
        {
            var problems = SimProfile.Problems($"dungeon=D\npull.1=/{key}=1");
            Assert.DoesNotContain(problems, p => p.Problem.Contains("is not a pull option", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Profile_Lines_Are_Spaces_At_The_Same_Offsets()
    {
        string text = Profile.ReplaceLineEndings("\r\n");
        var file = SimcFile.Of(null, text);
        Assert.Equal(text.Length, file.ActionList.Length);
        Assert.Equal([0, 1, 2], file.ProfileLines.Order());
        string[] lines = file.ActionList.Split('\n');
        Assert.All(lines[..3], line => Assert.Equal("", line.Trim()));
        Assert.Equal(text.Split('\n')[3..], lines[3..]);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Each_Line_Has_Tokens_And_A_Good_Profile_Line_No_Diagnostic(string newLine)
    {
        string text = Profile.ReplaceLineEndings(newLine);
        var check = Language.Check(null, text);
        Assert.NotNull(check);

        var data = SimcLanguageServer.SemanticTokenData(text, check);
        var lines = new List<int>();
        for (int i = 0, line = 0; i < data.Count; i += 5) lines.Add(line += data[i]);
        Assert.Equal([0, 1, 2, 3, 4, 5], lines.Distinct());

        var diagnostics = SimcLanguageServer.Diagnostics(text, check);
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.True((int)d!["range"]!["start"]!["line"]! >= 4));
    }

    [Fact]
    public void An_Action_Line_Completes_And_A_Profile_Line_Does_Not()
    {
        int afterBuff = Profile.IndexOf("buff.", StringComparison.Ordinal) + "buff.".Length;
        Assert.NotEmpty(Language.Complete(null, Profile, afterBuff, false)!.Items);
        Assert.Null(Language.Complete(null, Profile, Profile.IndexOf("161", StringComparison.Ordinal), true));
    }

    [Fact]
    public void The_Extensions_Token_Types_And_Colours_Are_Current()
    {
        string extension = Path.Combine(TestPaths.RepositoryRoot, "src", "FellowshipSim.VsCode");
        string css = Path.Combine(TestPaths.RepositoryRoot, "src", "FellowshipSim.UI", "wwwroot", "css");
        var package = JsonNode.Parse(File.ReadAllText(Path.Combine(extension, "package.json")))!.AsObject();
        var expected = package.DeepClone().AsObject();
        ExtensionContributes.Apply(expected, css);
        Assert.True(JsonNode.DeepEquals(expected, package),
            "package.json's token types or colours are out of date: run `npm run package` in src/FellowshipSim.VsCode.");
    }
}
