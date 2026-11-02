using System.Text.Json.Nodes;
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
    public void A_File_With_No_Hero_Has_No_Check()
    {
        Assert.Null(SimcFile.Of(null, "actions+=/x").Hero);
        Assert.Null(Language.Check(null, "actions+=/x"));
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
    public void No_Profile_Line_Has_A_Token_Or_A_Diagnostic(string newLine)
    {
        string text = Profile.ReplaceLineEndings(newLine);
        var check = Language.Check(null, text);
        Assert.NotNull(check);

        var data = SimcLanguageServer.SemanticTokenData(text, check);
        var lines = new List<int>();
        for (int i = 0, line = 0; i < data.Count; i += 5) lines.Add(line += data[i]);
        Assert.All(lines, line => Assert.True(line >= 3));
        Assert.Contains(3, lines);

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
