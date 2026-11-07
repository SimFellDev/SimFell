using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.APL.Editing;
using FellowshipSim.UI.Apl;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The completion list of the editor's code fields, and a pick written in place of the part at the caret.</summary>
public class CompletionsTests
{
    private readonly AplVocabulary _v = EditorVocabulary.Create();

    // The text with a pick written in place of the part at the caret, and the caret after it.
    private static (string Text, int Caret) Accept(string text, int at, Completion item)
    {
        var (from, to) = CodeEdits.PartAt(text, at);
        return (text[..from] + item.Written + text[to..], from + item.Written.Length);
    }

    [Fact]
    public void Empty_word_lists_every_kind_of_value_functions_and_operators_in_that_order()
    {
        string resourceScope = PropertyExpression.ResourcePrefix.TrimEnd('.');
        var expected = PropertyExpression.Scopes.Keys
            .Concat(_v.Resources.Select(r => r.Slug).Take(1))
            .Append(resourceScope)
            .Concat(_v.Resources.Select(r => r.Slug).Skip(1))
            .Concat(PropertyExpression.Named.Keys.Select(n => n.Split('.')[0]))
            .Concat(PropertyExpression.ActionValues.Keys)
            .Concat(_v.BuildNames.Where(b => b.Value.Count > 0).Select(b => b.Key))
            .Append(PropertyExpression.ActiveDotScope)
            .Append(PropertyExpression.VariableScope)
            .Append(PropertyExpression.PrevScope)
            .Append(PropertyExpression.PrevGcdScope)
            .Concat(Enum.GetValues<AplFunction>().Select(f => f.Name()))
            .Concat(["and", "or", "not"])
            .Distinct()
            .ToList();

        Assert.Equal(expected, Completions.For(_v, "", ["orbs"]).Select(c => c.Text).ToList());
        Assert.DoesNotContain(PropertyExpression.VariableScope, Completions.For(_v, "").Select(c => c.Text));
    }

    [Fact]
    public void Every_value_the_condition_builder_offers_completes()
    {
        string[] variables = ["orbs"];
        foreach (var kind in ConditionBuilder.Kinds(_v, variables))
        {
            foreach (var name in kind.Names)
            {
                var properties = kind.Properties is { Count: > 0 } p ? p : [null];
                foreach (string? property in properties)
                {
                    // A code field completes the path after an aggregate's ':' ("min:dot.chill.remains").
                    string path = ConditionBuilder.JoinPath(_v, new PathParts(kind.Id, name.Slug, property));
                    path = path[(path.IndexOf(':') + 1)..];
                    int dot = path.LastIndexOf('.');
                    var found = Completions.For(_v, path[..(dot + 1)], variables).Select(c => c.Text);
                    Assert.Contains(path[(dot + 1)..], found);
                }
            }
        }
    }

    [Fact]
    public void Build_parts_active_dots_variables_action_values_and_deficits_have_their_parts()
    {
        Assert.Equal(_v.BuildNames[PropertyExpression.TalentScope].Select(t => t.Slug), Completions.For(_v, "talent.").Select(c => c.Text));
        Assert.Equal(_v.Auras.Select(a => a.Slug), Completions.For(_v, "active_dot.").Select(c => c.Text));
        Assert.All(Completions.For(_v, "active_dot."), c => Assert.Equal(AplTokenKind.Debuff, c.Node.Kind));
        Assert.Equal(["orbs", "spent"], Completions.For(_v, "variable.", ["orbs", "spent"]).Select(c => c.Text));
        Assert.Contains("full_recharge_time", Completions.For(_v, "full_rech").Select(c => c.Text));
        Assert.Equal(["deficit"], Completions.For(_v, "winter_orbs.").Select(c => c.Text));
        Assert.Equal(["deficit"], Completions.For(_v, "resource.winter_orbs.").Select(c => c.Text));
        Assert.All(Completions.For(_v, "action.ice_comet."), c => Assert.Equal(AplTokenKind.Cooldown, c.Node.Kind));
    }

    [Fact]
    public void Starting_parts_come_before_containing_parts()
    {
        var found = Completions.For(_v, "w").Select(c => c.Text).ToList();

        int starting = found.TakeWhile(text => text.StartsWith('w')).Count();
        Assert.Equal(["weapon", "winter_orbs", "worb"], found.Take(starting).Order(StringComparer.Ordinal));
        Assert.All(found.Skip(starting), text => Assert.False(text.StartsWith('w')));
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
    public void A_debuffs_application_and_a_previous_action_have_their_parts()
    {
        Assert.Contains("oldest", Completions.For(_v, "debuff.chill.").Select(c => c.Text));
        Assert.Equal(["remains", "refreshable"], Completions.For(_v, "dot.chill.newest.").Select(c => c.Text));
        Assert.DoesNotContain("oldest", Completions.For(_v, "buff.chill.").Select(c => c.Text));
        Assert.Equal(_v.Abilities.Select(a => a.Slug), Completions.For(_v, "prev.").Select(c => c.Text));
        Assert.Equal(_v.Abilities.Select(a => a.Slug), Completions.For(_v, "prev_gcd.").Select(c => c.Text));
        Assert.Equal(AplTokenKind.Function, Completions.For(_v, "floo").Single().Node.Kind);
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
    public void A_list_opens_on_a_word_and_covers_the_part_at_the_caret()
    {
        Assert.Null(CodeEdits.Open(_v, "3", 1, true));
        Assert.Null(CodeEdits.Open(_v, "x and ", 6, false));
        Assert.Null(CodeEdits.Open(_v, "zzz", 3, false));

        var always = CodeEdits.Open(_v, "x and ", 6, true);
        Assert.NotNull(always);
        Assert.Equal(Completions.For(_v, "").Count, always.Items.Count);

        var list = CodeEdits.Open(_v, "x and bufxy", 8, false)!;
        Assert.Equal((6, 11), (list.From, list.To));
    }

    [Fact]
    public void A_list_reads_the_path_before_the_caret_only()
    {
        var list = CodeEdits.Open(_v, "buff.ch and x", 7, false)!;

        Assert.Equal("chill", Assert.Single(list.Items).Text);
        Assert.Equal((5, 7), (list.From, list.To));
    }

    [Fact]
    public void A_pick_replaces_the_part_at_the_caret_and_a_scope_is_written_with_its_dot()
    {
        var buff = Completions.For(_v, "bu").First();
        Assert.Equal(("x and buff.", 11), Accept("x and bu", 8, buff));
        Assert.Equal(("buff. and y", 5), Accept("bufxy and y", 2, buff));

        var chill = Completions.For(_v, "buff.c").First();
        Assert.Equal(("buff.chill.", 11), Accept("buff.c", 6, chill));

        var orbs = Completions.For(_v, "winter").First();
        Assert.Equal(("winter_orbs >= 3", 11), Accept("wi >= 3", 2, orbs));
    }
}
