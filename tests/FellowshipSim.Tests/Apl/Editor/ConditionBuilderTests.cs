using FellowshipSim.Core;
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
    [InlineData("(active_enemies - active_dot.chill) >= 1 and worb % 2 >= dungeon_event.adds.count")]
    [InlineData("cooldown.ice_comet.full_recharge_time <= (gcd.max * 3 + worb * 0.4) or not (worb - 1)")]
    [InlineData("worb < spirit and 3")]
    [InlineData("a < b <= c and dot.x.remains - 100 * (target.time_to_die >= 15) - 100 * (target.time_to_die >= 24)")]
    [InlineData("-not worb + not -spirit * --3 and not not a")]
    [InlineData("(a or b) * 2 = 0 and (a and (b or c)) > 1")]
    public void Rows_write_the_text_they_were_read_from(string text)
    {
        var rows = ConditionRow.From(AplCondition.Rows(AplParser.ParseExpression(text)));

        Assert.Equal(text, ConditionBuilder.Text(rows));
    }

    /// <summary>Every condition and target expression of every hero's built-in action lists has rows that write it as it reads.</summary>
    [Fact]
    public void Every_built_in_expression_has_rows_that_write_its_text()
    {
        var expressions = BuiltInActions().SelectMany(a => new[] { a.Action.Condition, a.Action.TargetExpression }).OfType<string>().ToList();

        Assert.NotEmpty(expressions);
        Assert.All(expressions, text => Assert.Equal(text, ConditionBuilder.Text(ConditionRow.From(AplCondition.Rows(AplParser.ParseExpression(text))))));
    }

    /// <summary>
    /// Random expressions over the whole grammar (names, numbers, parentheses, '!' and '-', arithmetic, chained comparisons,
    /// and 'and' and 'or' inside arithmetic) have rows that write the text the formatter writes.
    /// </summary>
    [Fact]
    public void Every_expression_of_the_grammar_has_rows_that_write_its_text()
    {
        var random = new Random(1);
        for (int i = 0; i < 2000; i++)
        {
            string text = AplFormatter.Format(Expression(random, 4), AplOperatorStyle.Words);
            var parsed = AplParser.ParseExpression(text);
            string expected = AplFormatter.Format(parsed, AplOperatorStyle.Words);

            Assert.Equal(expected, ConditionBuilder.Text(ConditionRow.From(AplCondition.Rows(parsed))));
        }
    }

    private static readonly string[] GeneratedPaths = ["worb", "buff.chill.up", "cooldown.ice_comet.remains", "talent.icy_talons"];

    private static IAplExpression Expression(Random random, int depth)
    {
        int pick = depth == 0 ? random.Next(2) : random.Next(6);
        return pick switch
        {
            0 => new PropertyExpression(GeneratedPaths[random.Next(GeneratedPaths.Length)]),
            1 => new ConstantExpression(new[] { 0, 1, 2.5, 100 }[random.Next(4)]),
            2 => new UnaryOpExpression(random.Next(2) == 0 ? UnaryOperator.Not : UnaryOperator.Minus, Expression(random, depth - 1)),
            3 => new GroupExpression(Expression(random, depth - 1)),
            _ => new BinaryOpExpression(Expression(random, depth - 1), Enum.GetValues<BinaryOperator>()[random.Next(Enum.GetValues<BinaryOperator>().Length)],
                Expression(random, depth - 1)),
        };
    }

    /// <summary>
    /// Every name in every built-in condition and target expression is a name of the hero and in its kind's names: the
    /// builder lists it for picking.
    /// </summary>
    [Fact]
    public void Every_name_in_a_built_in_expression_is_in_its_kind()
    {
        var missing = new List<string>();
        foreach (var hero in BuiltInActions().GroupBy(a => a.Hero))
        {
            var vocabulary = AplVocabulary.For(hero.Key);
            foreach (var list in hero.GroupBy(a => a.List))
            {
                var kinds = ConditionBuilder.Kinds(vocabulary, ConditionBuilder.Variables([new ActionList(list.Key, list.Select(a => EditorAction.From(a.Action)).ToList())]));
                foreach (string path in list.SelectMany(a => new[] { a.Action.Condition, a.Action.TargetExpression }).OfType<string>()
                             .SelectMany(text => Paths(AplCondition.Rows(AplParser.ParseExpression(text)))).Distinct())
                {
                    var parts = ConditionBuilder.SplitPath(vocabulary, path);
                    var kind = kinds.Single(k => k.Id == parts.Kind);
                    if (!ConditionBuilder.Knows(kind, parts.Name) || (kind.Properties != null && !kind.Properties.Contains(parts.Property!)))
                    {
                        missing.Add($"{hero.Key} {list.Key}: {path} ({parts.Kind})");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    private static IEnumerable<string> Paths(IEnumerable<AplConditionItem> rows) => rows.SelectMany(row =>
        row.Items is { } items ? Paths(items)
        : (row.Left ?? []).Concat((row.Comparisons ?? []).SelectMany(c => c.Terms)).SelectMany(Paths));

    private static IEnumerable<string> Paths(AplTerm term) =>
        term.Items is { } items ? Paths(items) : term.Path is { } path ? [path] : [];

    private static IEnumerable<(string Hero, string List, AplDocumentAction Action)> BuiltInActions() =>
        Catalog.Heroes.Keys.SelectMany(hero => DefaultActionLists.Names(hero)
            .SelectMany(name => AplDocument.Parse(DefaultActionLists.Read(hero, name)!).Select(action => (hero, name, action))));

    [Fact]
    public void A_term_without_a_value_and_a_row_or_group_without_such_terms_are_left_out()
    {
        List<ConditionRow> rows =
        [
            new() { Left = [new ConditionTerm()] },
            new()
            {
                Join = "or", Left = [new ConditionTerm { Path = "worb" }, new ConditionTerm { Operator = "+" }],
                Comparisons = [new ConditionComparison { Comparison = ">=" }],
            },
            new() { Join = "and", Items = [new ConditionRow { Left = [new ConditionTerm()] }] },
            new() { Join = null, Not = true, Items = [new ConditionRow { Left = [new ConditionTerm { Path = "a" }] }, new ConditionRow { Join = "or", Left = [new ConditionTerm { Path = "b" }] }] },
            new()
            {
                Left =
                [
                    new ConditionTerm(),
                    new ConditionTerm { Operator = "*", Number = 2, Prefix = "-" },
                    new ConditionTerm { Operator = "-", Items = [new ConditionRow { Left = [new ConditionTerm { Path = "c", Prefix = "!" }] }] },
                ],
            },
        ];

        Assert.Equal("worb >= 0 and not (a or b) and -2 - (not c)", ConditionBuilder.Text(rows));
    }

    [Fact]
    public void Paths_split_into_kind_name_and_property()
    {
        Assert.Equal(new PathParts("buff", "chill", "up"), ConditionBuilder.SplitPath(_v, "buff.chill.up"));
        Assert.Equal(new PathParts(ConditionBuilder.ResourceKind, "worb", ConditionBuilder.CurrentProperty), ConditionBuilder.SplitPath(_v, PropertyExpression.ResourcePrefix + "worb"));
        Assert.Equal(new PathParts(ConditionBuilder.ResourceKind, "winter_orbs", ConditionBuilder.DeficitProperty), ConditionBuilder.SplitPath(_v, "winter_orbs.deficit"));
        Assert.Equal(new PathParts(ConditionBuilder.NamedKind, "target.health.pct"), ConditionBuilder.SplitPath(_v, "target.health.pct"));
        Assert.Equal(new PathParts(ConditionBuilder.ActionValueKind, "full_recharge_time"), ConditionBuilder.SplitPath(_v, "full_recharge_time"));
        Assert.Equal(new PathParts(PropertyExpression.TalentScope, "icy_talons"), ConditionBuilder.SplitPath(_v, "talent.icy_talons"));
        Assert.Equal(new PathParts(PropertyExpression.ActiveDotScope, "chill"), ConditionBuilder.SplitPath(_v, "active_dot.chill"));
        Assert.Equal(new PathParts(PropertyExpression.VariableScope, "x"), ConditionBuilder.SplitPath(_v, "variable.x"));

        Assert.Equal("cooldown.ice_comet.ready", ConditionBuilder.JoinPath(_v, new PathParts("cooldown", "ice_comet", "ready")));
        Assert.Equal("worb", ConditionBuilder.JoinPath(_v, new PathParts(ConditionBuilder.ResourceKind, "worb", ConditionBuilder.CurrentProperty)));
        Assert.Equal("worb.deficit", ConditionBuilder.JoinPath(_v, new PathParts(ConditionBuilder.ResourceKind, "worb", ConditionBuilder.DeficitProperty)));
        Assert.Equal("weapon.chronoshift", ConditionBuilder.JoinPath(_v, new PathParts(PropertyExpression.WeaponScope, "chronoshift")));
        Assert.Equal("charges", ConditionBuilder.JoinPath(_v, new PathParts(ConditionBuilder.ActionValueKind, "charges")));
    }

    [Fact]
    public void Kinds_are_the_scopes_resource_fight_value_action_build_enemies_and_variables()
    {
        var kinds = ConditionBuilder.Kinds(_v, ["will_spend_orbs"]);

        Assert.Equal(
            [
                .. PropertyExpression.Scopes.Keys, ConditionBuilder.ResourceKind, ConditionBuilder.NamedKind, ConditionBuilder.ActionValueKind,
                .. PropertyExpression.BuildScopes.Keys, PropertyExpression.ActiveDotScope, PropertyExpression.VariableScope,
            ],
            kinds.Select(k => k.Id));
        Assert.Equal("Debuff on target", kinds.Single(k => k.Id == PropertyExpression.DebuffScope).Label);
        Assert.Equal(_v.Abilities, kinds.Single(k => k.Id == PropertyExpression.CooldownScope).Names);
        Assert.Equal(["will_spend_orbs"], kinds.Single(k => k.Id == PropertyExpression.VariableScope).Names.Select(n => n.Slug));
        Assert.Equal(PropertyExpression.ActionValues.Keys, kinds.Single(k => k.Id == ConditionBuilder.ActionValueKind).Names.Select(n => n.Slug));
    }

    [Fact]
    public void Variables_are_the_names_the_variable_actions_set()
    {
        var actions = AplDocument.Parse("actions=variable,name=a,value=1\nactions+=/frost_bolt\nactions+=/variable,name=b,op=reset\nactions+=/variable,name=a,value=2");

        Assert.Equal(["a", "b"], ConditionBuilder.Variables([new ActionList("default", actions.Select(EditorAction.From).ToList())]));
    }

    /// <summary>The value picker colours each kind with --kind-&lt;id&gt;; app.css defines one for every kind, each a different value.</summary>
    [Fact]
    public void Every_kind_has_its_own_colour()
    {
        string css = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "FellowshipSim.UI", "wwwroot", "css", "app.css"));
        var colours = ConditionBuilder.Kinds(_v, []).ToDictionary(k => k.Id, k =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(css, $@"--kind-{k.Id}:\s*([^;]+);");
            Assert.True(match.Success, $"app.css has no --kind-{k.Id}.");
            return match.Groups[1].Value.Trim();
        });

        Assert.Equal(colours.Count, colours.Values.Distinct().Count());
    }

    [Fact]
    public void A_shared_name_shows_its_slug()
    {
        var resource = ConditionBuilder.Kinds(_v, []).Single(k => k.Id == ConditionBuilder.ResourceKind);

        Assert.Equal("Winter Orbs (worb)", ConditionBuilder.NameLabel(resource, resource.Names.Single(n => n.Slug == "worb")));
        var buff = ConditionBuilder.Kinds(_v, []).First();
        Assert.Equal("Chill", ConditionBuilder.NameLabel(buff, buff.Names.First()));
    }

    /// <summary>A group, a condition in parentheses on a row's line and one on lines of its own write their text, open or closed.</summary>
    [Theory]
    [InlineData("(worb >= 3 or buff.chill.up) and not (a and (b or c))")]
    [InlineData("(active_enemies - active_dot.chill) >= 1")]
    [InlineData("(a or b and (c or d)) * 2 = 0")]
    public void Groups_write_their_text_open_or_closed(string text)
    {
        var rows = ConditionRow.From(AplCondition.Rows(AplParser.ParseExpression(text)));
        Assert.Equal(text, ConditionBuilder.Text(rows));

        foreach (var group in ConditionBuilder.Groups(rows)) group.Collapsed = true;
        var copy = rows.Select(r => r.Clone()).ToList();

        Assert.Equal(text, ConditionBuilder.Text(rows));
        Assert.Equal(text, ConditionBuilder.Text(copy));
        Assert.All(ConditionBuilder.Groups(copy), group => Assert.True(group.Collapsed));
    }

    [Fact]
    public void Groups_are_every_group_at_every_depth()
    {
        var rows = ConditionRow.From(AplCondition.Rows(AplParser.ParseExpression("(a or (b and c)) and d * (e or f and (g or h)) > 1")));

        Assert.Equal(["a or (b and c)", "b and c", "g or h"], ConditionBuilder.Groups(rows).Select(g => ConditionBuilder.Text(g.Items!)));
    }

    /// <summary>Each kind's names are coloured as the text colours them.</summary>
    [Fact]
    public void Each_kind_has_the_token_kind_of_its_names()
    {
        var tokens = new HashSet<AplTokenKind>();
        foreach (string hero in Catalog.Heroes.Keys)
        {
            var v = AplVocabulary.For(hero);
            foreach (var kind in ConditionBuilder.Kinds(v, ["x"]).Where(k => k.Names.Count > 0))
            {
                string path = ConditionBuilder.JoinPath(v, new PathParts(kind.Id, kind.Names[0].Slug, kind.Properties?.FirstOrDefault()));

                Assert.True(v.Classify(new PropertyExpression(path)).Kind == kind.Token, $"{hero} {kind.Label}: {path}");
                tokens.Add(kind.Token);
            }
        }

        Assert.Equal([AplTokenKind.Named, AplTokenKind.Resource, AplTokenKind.Buff, AplTokenKind.Debuff, AplTokenKind.Cooldown], tokens.Order());
    }

    [Fact]
    public void A_picked_name_takes_its_kinds_first_property_or_keeps_the_property_it_has()
    {
        var kinds = ConditionBuilder.Kinds(_v, []);
        var aura = kinds.Single(k => k.Id == PropertyExpression.BuffScope).Properties!;
        var term = new ConditionTerm { Number = 4 };

        ConditionBuilder.Apply(term, new ValuePick(PropertyExpression.BuffScope, "chill"), _v, kinds);
        Assert.Equal($"buff.chill.{aura[0]}", term.Path);
        Assert.Null(term.Number);

        term.Path = $"buff.chill.{aura[1]}";
        ConditionBuilder.Apply(term, new ValuePick(PropertyExpression.BuffScope, "glacial_assault"), _v, kinds);
        Assert.Equal($"buff.glacial_assault.{aura[1]}", term.Path);

        ConditionBuilder.Apply(term, new ValuePick(ConditionBuilder.ResourceKind, "worb"), _v, kinds);
        Assert.Equal("worb", term.Path);

        ConditionBuilder.Apply(term, new ValuePick(PropertyExpression.TalentScope, "icy_talons"), _v, kinds);
        Assert.Equal("talent.icy_talons", term.Path);
    }

    [Fact]
    public void A_picked_number_group_or_prefix_changes_the_term()
    {
        var kinds = ConditionBuilder.Kinds(_v, []);
        var term = new ConditionTerm { Operator = "*", Prefix = "-", Path = "worb" };

        ConditionBuilder.Apply(term, new ValuePick(ConditionBuilder.PrefixKind, Prefix: "!"), _v, kinds);
        Assert.Equal("!-", term.Prefix);

        ConditionBuilder.Apply(term, new ValuePick(ConditionBuilder.GroupKind), _v, kinds);
        Assert.Equal(("*", "!-", null, null), (term.Operator, term.Prefix, term.Path, term.Number));
        var held = Assert.Single(Assert.Single(term.Items!).Left!);
        Assert.Equal((null, null, "worb"), (held.Operator, held.Prefix, held.Path));
        Assert.Equal("a * not -(worb)", ConditionBuilder.Text([new ConditionTerm { Path = "a" }, term]));

        ConditionBuilder.Apply(term, new ValuePick(ConditionBuilder.NumberKind), _v, kinds);
        Assert.Equal(("*", "!-", null, 0d, null), (term.Operator, term.Prefix, term.Path, term.Number, term.Items));
    }

    [Fact]
    public void A_pick_on_a_groups_empty_row_makes_a_group_number_or_name_row()
    {
        var kinds = ConditionBuilder.Kinds(_v, []);
        var aura = kinds.Single(k => k.Id == PropertyExpression.BuffScope).Properties!;

        var group = ConditionBuilder.RowFor(new ValuePick(ConditionBuilder.GroupKind), "or", true, _v, kinds);
        Assert.Equal(("or", true, 0), (group.Join, group.Not, group.Items!.Count));
        Assert.Null(group.Left);

        var number = ConditionBuilder.RowFor(new ValuePick(ConditionBuilder.NumberKind), "and", false, _v, kinds);
        Assert.Equal(0d, Assert.Single(number.Left!).Number);
        Assert.Null(number.Items);

        var name = ConditionBuilder.RowFor(new ValuePick(PropertyExpression.BuffScope, "chill"), "and", true, _v, kinds);
        Assert.Equal($"buff.chill.{aura[0]}", Assert.Single(name.Left!).Path);
        Assert.Equal($"not buff.chill.{aura[0]}", ConditionBuilder.Text([name]));
    }
}
