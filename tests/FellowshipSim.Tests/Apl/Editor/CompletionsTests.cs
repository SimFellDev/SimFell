using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.UI.Apl;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The completion list of the editor's code fields, and a pick written in place of the part at the caret.</summary>
public class CompletionsTests
{
    private readonly AplVocabulary _v = EditorVocabulary.Create();

    private static CaretPlace Caret(int at, double left = 0, double width = 600, bool focused = true) => new(at, left, 20, width, focused);

    [Fact]
    public void Empty_word_lists_scopes_resources_named_values_and_operators_in_that_order()
    {
        string resourceScope = PropertyExpression.ResourcePrefix.TrimEnd('.');
        var expected = PropertyExpression.Scopes.Keys
            .Concat(_v.Resources.Select(r => r.Slug).Take(1))
            .Append(resourceScope)
            .Concat(_v.Resources.Select(r => r.Slug).Skip(1))
            .Concat(PropertyExpression.Named.Keys.Select(n => n.Split('.')[0]))
            .Concat(["and", "or", "not"])
            .Distinct()
            .ToList();

        Assert.Equal(expected, Completions.For(_v, "").Select(c => c.Text).ToList());
    }

    [Fact]
    public void Starting_parts_come_before_containing_parts()
    {
        var found = Completions.For(_v, "w").Select(c => c.Text).ToList();

        Assert.Equal(["winter_orbs", "worb"], found.Take(2));
        Assert.All(found.Skip(2), text => Assert.False(text.StartsWith('w')));
        Assert.All(found, text => Assert.Contains("w", text));
    }

    [Fact]
    public void Parts_after_a_dot_are_the_children_and_the_word_is_lower_cased()
    {
        var names = Completions.For(_v, "BUFF.").ToList();

        Assert.Equal(_v.Auras.Select(a => a.Slug), names.Select(c => c.Text));
        Assert.All(names, c => Assert.Equal(AplTokenKind.Buff, c.Node.Kind));
        Assert.Equal("Glacial Assault", names.Single(c => c.Text == "glacial_assault").Node.Detail);
        Assert.Equal(PropertyExpression.Scopes[PropertyExpression.CooldownScope],
            Completions.For(_v, "cooldown.ice_comet.").Select(c => c.Text));
        Assert.Empty(Completions.For(_v, "nothing."));
    }

    [Fact]
    public void A_finished_name_completes_nothing_and_a_name_with_parts_after_it_has_more()
    {
        Assert.Empty(Completions.For(_v, "and"));
        Assert.Empty(Completions.For(_v, "buff.chill.up"));

        Assert.Equal(["buff", "debuff"], Completions.For(_v, "buff").Select(c => c.Text));
        Assert.True(Completions.For(_v, "buff")[0].HasMore);
        var resource = Completions.For(_v, "winter").First();
        Assert.Equal("winter_orbs", resource.Text);
        Assert.False(resource.HasMore);
        Assert.Equal("Winter Orbs", resource.Node.Detail);
    }

    [Fact]
    public void A_list_opens_on_a_focused_word_and_stays_inside_the_field()
    {
        Assert.Null(CodeEdits.Open(_v, "bu", Caret(2, focused: false), false));
        Assert.Null(CodeEdits.Open(_v, "3", Caret(1), true));
        Assert.Null(CodeEdits.Open(_v, "x and ", Caret(6), false));
        Assert.Null(CodeEdits.Open(_v, "zzz", Caret(3), false));

        var always = CodeEdits.Open(_v, "x and ", Caret(6), true);
        Assert.NotNull(always);
        Assert.Equal(Completions.For(_v, "").Count, always.Items.Count);

        var list = CodeEdits.Open(_v, "bu", Caret(2, left: 500, width: 600), false)!;
        Assert.Equal(0, list.Selected);
        Assert.Equal(600 - CodeEdits.ListWidth, list.Left);
        Assert.Equal(24, list.Top);
        Assert.Equal(0, CodeEdits.Open(_v, "bu", Caret(2, left: 10, width: 100), false)!.Left);
    }

    [Fact]
    public void A_list_reads_the_path_before_the_caret_only()
    {
        var list = CodeEdits.Open(_v, "buff.ch and x", Caret(7), false)!;

        Assert.Equal("chill", Assert.Single(list.Items).Text);
    }

    [Fact]
    public void A_pick_replaces_the_part_at_the_caret_and_a_scope_is_written_with_its_dot()
    {
        var buff = Completions.For(_v, "bu").First();
        Assert.Equal(("x and buff.", 11), CodeEdits.Accept("x and bu", 8, buff));
        Assert.Equal(("buff. and y", 5), CodeEdits.Accept("bufxy and y", 2, buff));

        var chill = Completions.For(_v, "buff.c").First();
        Assert.Equal(("buff.chill.", 11), CodeEdits.Accept("buff.c", 6, chill));

        var orbs = Completions.For(_v, "winter").First();
        Assert.Equal(("winter_orbs >= 3", 11), CodeEdits.Accept("wi >= 3", 2, orbs));
    }
}
