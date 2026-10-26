using System.Text.RegularExpressions;

namespace FellowshipSim.Tests.Shell;

/// <summary>Every tooltip in FellowshipSim.UI is the app's own (data-tooltip or the Tooltip component).</summary>
public partial class TooltipTests
{
    // A title attribute in markup, in a RenderTreeBuilder call, or set from a script.
    [GeneratedRegex(@"\stitle\s*=\s*""|AddAttribute\(\d+,\s*""title""|setAttribute\([""']title|\.title\s*=")]
    private static partial Regex TitleAttribute();

    [Fact]
    public void No_component_or_script_has_a_title_attribute()
    {
        string ui = Path.Combine(TestPaths.RepositoryRoot, "src", "FellowshipSim.UI");
        var titles = Directory.EnumerateFiles(ui, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".razor" or ".cs" or ".js")
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadLines(path).Select((line, i) => (path, line, i)))
            .Where(l => TitleAttribute().IsMatch(l.line))
            .Select(l => $"{Path.GetRelativePath(ui, l.path)}:{l.i + 1}: {l.line.Trim()}")
            .ToList();
        Assert.True(titles.Count == 0, string.Join(Environment.NewLine, titles));
    }
}
