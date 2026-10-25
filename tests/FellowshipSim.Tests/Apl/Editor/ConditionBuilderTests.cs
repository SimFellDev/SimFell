using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.UI.Apl;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The condition builder's rows: read from text, written back as text, and their paths in parts.</summary>
public class ConditionBuilderTests
{
    private readonly AplVocabulary _v = EditorVocabulary.Create();

    [Theory]
    [InlineData("worb >= 3")]
    [InlineData("worb >= 3 and not buff.chill.up")]
    [InlineData("not (worb < 2) or buff.chill.up and cooldown.ice_comet.ready")]
    [InlineData("active_enemies > 2 and (buff.chill.stack = 3 or not (worb != 0.5))")]
    [InlineData("worb > -1")]
    public void Rows_write_the_text_they_were_read_from(string text)
    {
        var rows = ConditionRow.From(AplCondition.Rows(AplParser.ParseExpression(text))!);

        Assert.Equal(text, ConditionBuilder.Text(rows));
    }

    [Fact]
    public void A_row_without_a_path_and_a_group_without_such_rows_are_left_out()
    {
        List<ConditionRow> rows =
        [
            new() { Kind = ConditionBuilder.ResourceKind },
            new() { Join = "or", Path = "worb", Comparison = ">=", Value = null },
            new() { Join = "and", Items = [new ConditionRow { Kind = "buff" }] },
            new() { Join = null, Not = true, Items = [new ConditionRow { Path = "a" }, new ConditionRow { Join = "or", Path = "b" }] },
        ];

        Assert.Equal("worb >= 0 and not (a or b)", ConditionBuilder.Text(rows));
    }

    [Fact]
    public void Paths_split_into_scope_name_and_property_or_a_resource_or_a_named_value()
    {
        Assert.Equal(new PathParts("buff", "chill", "up"), ConditionBuilder.SplitPath(_v, "buff.chill.up"));
        Assert.Equal(new PathParts(ConditionBuilder.ResourceKind, "worb"), ConditionBuilder.SplitPath(_v, PropertyExpression.ResourcePrefix + "worb"));
        Assert.Equal(new PathParts(ConditionBuilder.ResourceKind, "winter_orbs"), ConditionBuilder.SplitPath(_v, "winter_orbs"));
        Assert.Equal(new PathParts(ConditionBuilder.NamedKind, "target.health.pct"), ConditionBuilder.SplitPath(_v, "target.health.pct"));

        Assert.Equal("cooldown.ice_comet.ready", ConditionBuilder.JoinPath(_v, new PathParts("cooldown", "ice_comet", "ready")));
        Assert.Equal("worb", ConditionBuilder.JoinPath(_v, new PathParts(ConditionBuilder.ResourceKind, "worb")));
    }

    [Fact]
    public void Kinds_are_the_scopes_then_resource_and_fight_value()
    {
        var kinds = ConditionBuilder.Kinds(_v);

        Assert.Equal([.. PropertyExpression.Scopes.Keys, ConditionBuilder.ResourceKind, ConditionBuilder.NamedKind], kinds.Select(k => k.Id));
        Assert.Equal("Debuff on target", kinds.Single(k => k.Id == PropertyExpression.DebuffScope).Label);
        Assert.Equal(_v.Abilities, kinds.Single(k => k.Id == PropertyExpression.CooldownScope).Names);
    }

    [Fact]
    public void A_shared_name_shows_its_slug()
    {
        var resource = ConditionBuilder.Kinds(_v).Single(k => k.Id == ConditionBuilder.ResourceKind);

        Assert.Equal("Winter Orbs (worb)", ConditionBuilder.NameLabel(resource, resource.Names.Single(n => n.Slug == "worb")));
        var buff = ConditionBuilder.Kinds(_v).First();
        Assert.Equal("Chill", ConditionBuilder.NameLabel(buff, buff.Names.First()));
    }
}
