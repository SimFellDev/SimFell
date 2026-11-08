using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;
using Superpower;

namespace FellowshipSim.Tests;

/// <summary>The formatter, target selection, and the document and vocabulary the visual action-list editor reads.</summary>
public class AplEditorTests
{
    [Theory]
    [InlineData("a&b|c", "a & b | c")]
    [InlineData("a|b&c", "a | b & c")]
    [InlineData("(a|b)&c", "(a | b) & c")]
    [InlineData("!a&b", "!a & b")]
    [InlineData("!(a|b)", "!(a | b)")]
    [InlineData("!(a>b)", "!(a > b)")]
    [InlineData("!a>b", "!a > b")]
    [InlineData("-x<-(y)", "-x < -(y)")]
    [InlineData("--1", "--1")]
    [InlineData("!-x", "!-x")]
    [InlineData("-(a+b)", "-(a + b)")]
    [InlineData("a+b*c", "a + b * c")]
    [InlineData("(a+b)*c", "(a + b) * c")]
    [InlineData("a-b-c", "a - b - c")]
    [InlineData("a-(b-c)", "a - (b - c)")]
    [InlineData("a%b%%c", "a % b %% c")]
    [InlineData("a%(b%%c)", "a % (b %% c)")]
    [InlineData("a--b", "a - -b")]
    [InlineData("a+1>b*2&c", "a + 1 > b * 2 & c")]
    [InlineData("(a<b)<c", "(a < b) < c")]
    [InlineData("a<(b<c)", "a < (b < c)")]
    [InlineData("x<=2&y>-1.5", "x <= 2 & y > -1.5")]
    [InlineData("x==1|x=2|x!=3", "x = 1 | x = 2 | x != 3")]
    [InlineData("buff.glacial_assault.stack>=3", "buff.glacial_assault.stack >= 3")]
    [InlineData("winter_orbs > 3 and spirit < 100", "winter_orbs > 3 & spirit < 100")]
    [InlineData("a OR not b And c", "a | !b & c")]
    [InlineData("android|orb.notch", "android | orb.notch")]
    [InlineData("winter_orbs=5|(anima>=9&winter_orbs>4)", "winter_orbs = 5 | (anima >= 9 & winter_orbs > 4)")]
    [InlineData("((a))", "((a))")]
    public void Formatter_Keeps_Written_Parentheses_And_Reads_Back_The_Same(string text, string expected)
    {
        var parsed = AplParser.ParseExpression(text);
        string formatted = AplFormatter.Format(parsed);
        Assert.Equal(expected, formatted);
        Assert.Equal(parsed.ToString(), AplParser.ParseExpression(formatted).ToString());
    }

    [Theory]
    [InlineData("a&!b|c", "a and not b or c")]
    [InlineData("!(a|b)", "not (a or b)")]
    [InlineData("!!a", "not not a")]
    [InlineData("a!=1&-b<2", "a != 1 and -b < 2")]
    [InlineData("winter_orbs=5|(anima>=9&winter_orbs>4)", "winter_orbs = 5 or (anima >= 9 and winter_orbs > 4)")]
    public void Formatter_Writes_Words_For_The_Editor(string text, string expected)
    {
        var parsed = AplParser.ParseExpression(text);
        string words = AplFormatter.Format(parsed, AplOperatorStyle.Words);
        Assert.Equal(expected, words);
        Assert.Equal(AplFormatter.Format(parsed), AplFormatter.Format(AplParser.ParseExpression(words)));
    }

    [Fact]
    public void Formatter_Adds_The_Parentheses_A_Built_Expression_Needs()
    {
        var a = new PropertyExpression("a");
        var b = new PropertyExpression("b");
        var expression = new BinaryOpExpression(new BinaryOpExpression(a, BinaryOperator.Or, b), BinaryOperator.And, new UnaryOpExpression(UnaryOperator.Not, new BinaryOpExpression(a, BinaryOperator.And, b)));
        Assert.Equal("(a | b) & !(a & b)", AplFormatter.Format(expression));
    }

    [Theory]
    [InlineData("a&b", "a and b")]
    [InlineData("a & !b", "a and not b")]
    [InlineData("a&!(b|c)", "a and not (b or c)")]
    [InlineData("(!a)|b", "(not a) or b")]
    [InlineData("a!=1 & b", "a!=1 and b")]
    [InlineData("a and  b", "a and  b")]
    public void Check_Writes_The_Text_In_Words(string text, string expected) =>
        Assert.Equal(expected, AplVocabulary.For("Rime").Check(text).InWords);

    [Theory]
    [InlineData("actions+=/frost_bolt,target_if=min:target.health.pct", "frost_bolt,target_if=min:target.health.pct")]
    [InlineData("actions+=/frost_bolt,target_if=MAX:debuff.x.stack,if=a", "frost_bolt,target_if=max:debuff.x.stack,if=a")]
    [InlineData("actions+=/frost_bolt,target_if=debuff.x.down", "frost_bolt,target_if=first:debuff.x.down")]
    [InlineData("actions+=/frost_bolt,target_if=first:debuff.x.down", "frost_bolt,target_if=first:debuff.x.down")]
    [InlineData("actions+=/frost_bolt,target_if=min:a+1", "frost_bolt,target_if=min:(a + 1)")]
    public void ActionLine_Target_If(string line, string expected) =>
        Assert.Equal(expected, AplParser.ParseActionLine(line)!.ToString());

    [Theory]
    [InlineData("actions+=/frost_bolt,if=min:")]
    [InlineData("actions+=/frost_bolt,target_if=lowest:a")]
    [InlineData("actions+=/frost_bolt,target_if=min:")]
    public void ActionLine_Malformed_Target_If_Throws(string line) =>
        Assert.Throws<ParseException>(() => AplParser.ParseActionLine(line));

    [Theory]
    [InlineData("min:target.health.pct", 2)]
    [InlineData("max:target.health.pct", 1)]
    [InlineData("target.health.pct<60", 2)]
    [InlineData("first:target.health.pct<40&target.health.pct>20", 4)]
    public void Target_If_Picks_The_Enemy(string targetIf, int expectedInstance)
    {
        var (hero, ctx) = Fight(90, 20, 50, 25);
        var action = AplParser.ParseActionLine($"frost_bolt,target_if={targetIf}")!;
        var picked = action.TargetIf!.Select(hero, ctx, ctx.ActiveEnemies);
        Assert.Equal(expectedInstance, picked!.InstanceId);
        Assert.Equal(1, ctx.Player.CurrentTarget.InstanceId);
    }

    [Fact]
    public void Target_If_First_Without_A_Match_Skips_The_Action()
    {
        var (hero, ctx) = Fight(90, 80);
        Assert.False(AplParser.ParseActionLine("frost_bolt,target_if=target.health.pct<50")!.CanExecute(hero, ctx));
        Assert.True(AplParser.ParseActionLine("frost_bolt,target_if=target.health.pct<85")!.CanExecute(hero, ctx));
    }

    [Fact]
    public void Condition_Reads_The_Picked_Target()
    {
        var (hero, ctx) = Fight(90, 20);
        Assert.True(AplParser.ParseActionLine("frost_bolt,target_if=min:target.health.pct,if=target.health.pct<50")!.CanExecute(hero, ctx));
        Assert.False(AplParser.ParseActionLine("frost_bolt,if=target.health.pct<50")!.CanExecute(hero, ctx));
    }

    [Fact]
    public void Target_If_Casts_At_The_Picked_Target()
    {
        var (hero, ctx) = Fight(90, 20);
        Assert.True(AplParser.ParseActionLine("frost_bolt,target_if=min:target.health.pct")!.Execute(hero, ctx));
        Assert.Equal(2, hero.CurrentCast!.Target!.InstanceId);
        Assert.Equal(1, ctx.Player.CurrentTarget.InstanceId);
    }

    [Fact]
    public void Cooldown_Reads_The_Ability_By_Its_Action_List_Name()
    {
        var (hero, ctx) = Fight(100);
        var charges = AplParser.ParseExpression("cooldown.cold_snap.charges");
        Assert.Equal(ColdSnap.AbilityCharges, charges.EvaluateNumeric(hero, ctx));
        Assert.True(AplParser.ParseActionLine("cold_snap")!.Execute(hero, ctx));
        ctx.RunUntil(SimTime.FromSeconds(1));
        Assert.Equal(ColdSnap.AbilityCharges - 1, charges.EvaluateNumeric(hero, ctx));
        Assert.True(AplParser.ParseExpression("cooldown.cold_snap.ready").Evaluate(hero, ctx));
    }

    [Fact]
    public void Cooldown_Of_The_Weapon_Ability_Is_Up_Until_It_Is_Cast()
    {
        var (hero, ctx) = Fight(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = Chronoshift.AbilityId }, 100);
        var up = AplParser.ParseExpression("cooldown.chronoshift.up");
        Assert.True(up.Evaluate(hero, ctx));
        Assert.True(AplParser.ParseActionLine("chronoshift")!.Execute(hero, ctx));
        ctx.RunUntil(SimTime.FromSeconds(5));
        Assert.False(up.Evaluate(hero, ctx));
    }

    [Fact]
    public void Document_Round_Trips_Text()
    {
        const string text = """
            # opener
            # second line
            actions+=/ice_comet,if=winter_orbs>=3&!buff.glacial_assault.up

            actions.aoe+=/bursting_ice,target_if=min:debuff.x.stack
            actions+=/run_action_list,name=aoe,if=active_enemies>=3
            actions+=/frost_bolt,line_cd=5
            # trailing
            """;
        var actions = AplDocument.Parse(text);
        Assert.Equal(
            [
                new AplDocumentAction("ice_comet", "winter_orbs >= 3 and not buff.glacial_assault.up", Comment: "opener\nsecond line"),
                new AplDocumentAction("bursting_ice", null, AplTargetSelection.Min, "debuff.x.stack", List: "aoe"),
                new AplDocumentAction("run_action_list", Condition: "active_enemies >= 3", ActionList: "aoe"),
                new AplDocumentAction("frost_bolt", Comment: "trailing", Arguments: "line_cd=5"),
            ],
            actions);

        // Each list's lines are together, the default list first.
        var (written, errors) = AplDocument.Write(actions);
        Assert.Empty(errors);
        Assert.Equal("""
            # opener
            # second line
            actions+=/ice_comet,if=winter_orbs >= 3 & !buff.glacial_assault.up
            actions+=/run_action_list,name=aoe,if=active_enemies >= 3
            # trailing
            actions+=/frost_bolt,line_cd=5

            actions.aoe+=/bursting_ice,target_if=min:debuff.x.stack

            """.ReplaceLineEndings("\n"), written);
        Assert.Equal(actions.OrderBy(a => a.List == "aoe"), AplDocument.Parse(written!));

        var lists = AplParser.ParseScript(written!);
        Assert.Equal(["ice_comet", "run_action_list", "frost_bolt"], lists.Actions.Select(a => a.AbilityName));
        Assert.Equal(["bursting_ice"], lists.Lists["aoe"].Select(a => a.AbilityName));
    }

    [Fact]
    public void Document_Holds_Use_Off_Gcd_And_Use_While_Casting_As_Switches()
    {
        const string text = """
            actions+=/ice_blitz,use_off_gcd=1,use_while_casting=1,line_cd=5,if=buff.chill.up
            actions+=/frost_bolt,use_off_gcd=0
            actions+=/call_action_list,name=aoe,use_off_gcd=1
            actions.aoe+=/frost_bolt
            """;
        var actions = AplDocument.Parse(text);
        Assert.Equal(new AplDocumentAction("ice_blitz", "buff.chill.up", Arguments: "line_cd=5", UseOffGcd: true, UseWhileCasting: true), actions[0]);
        Assert.Equal(new AplDocumentAction("frost_bolt"), actions[1]);
        // A list action keeps the argument as written: only an ability action reads it.
        Assert.Equal((false, "use_off_gcd=1"), (actions[2].UseOffGcd, actions[2].Arguments));

        var (written, errors) = AplDocument.Write(actions);
        Assert.Empty(errors);
        Assert.Equal("""
            actions+=/ice_blitz,line_cd=5,use_off_gcd=1,use_while_casting=1,if=buff.chill.up
            actions+=/frost_bolt
            actions+=/call_action_list,name=aoe,use_off_gcd=1

            actions.aoe+=/frost_bolt

            """.ReplaceLineEndings("\n"), written);
        var iceBlitz = AplParser.ParseScript(written!).Actions[0];
        Assert.True(iceBlitz.UseOffGcd);
        Assert.True(iceBlitz.UseWhileCasting);
    }

    [Fact]
    public void Document_Round_Trips_A_Variable_Action()
    {
        const string text = """
            actions+=/variable,name=orbs,value=winter_orbs+1,default=2,line_cd=5
            actions+=/variable,name=orbs,op=add,value=1,if=buff.ice_blitz.up
            actions+=/variable,op=reset,name=orbs
            actions+=/variable,name=orbs,op=set,value=0,default=0
            """;
        var actions = AplDocument.Parse(text);
        Assert.Equal(
            [
                new AplDocumentAction("variable", Arguments: "line_cd=5", Variable: new AplDocumentVariable("orbs", Value: "winter_orbs + 1", Default: 2)),
                new AplDocumentAction("variable", "buff.ice_blitz.up", Variable: new AplDocumentVariable("orbs", AplVariableOp.Add, "1")),
                new AplDocumentAction("variable", Variable: new AplDocumentVariable("orbs", AplVariableOp.Reset)),
                new AplDocumentAction("variable", Variable: new AplDocumentVariable("orbs", Value: "0")),
            ],
            actions);

        // The op is written when it is not set, and the default when it is not 0.
        var (written, errors) = AplDocument.Write(actions);
        Assert.Empty(errors);
        Assert.Equal("""
            actions+=/variable,name=orbs,value=winter_orbs + 1,default=2,line_cd=5
            actions+=/variable,name=orbs,value=1,op=add,if=buff.ice_blitz.up
            actions+=/variable,name=orbs,op=reset
            actions+=/variable,name=orbs,value=0

            """.ReplaceLineEndings("\n"), written);
        Assert.Equal(actions, AplDocument.Parse(written!));

        var variables = AplParser.ParseScript(written!).Actions.Select(a => a.Variable!).ToList();
        Assert.Equal([AplVariableOp.Set, AplVariableOp.Add, AplVariableOp.Reset, AplVariableOp.Set], variables.Select(v => v.Op));
        Assert.Equal([2, 0, 0, 0], variables.Select(v => v.Default));
    }

    [Fact]
    public void Document_Write_Reports_Each_Bad_Variable_Field()
    {
        var (text, errors) = AplDocument.Write(
        [
            new AplDocumentAction("variable"),
            new AplDocumentAction("variable", Variable: new AplDocumentVariable("my orbs", Value: "1")),
            new AplDocumentAction("variable", Variable: new AplDocumentVariable("orbs", AplVariableOp.Add)),
            new AplDocumentAction("variable", Variable: new AplDocumentVariable("orbs", Value: "(1")),
            new AplDocumentAction("variable", Variable: new AplDocumentVariable("orbs", AplVariableOp.Reset)),
        ]);
        Assert.Null(text);
        Assert.Equal([(0, "variable"), (1, "variable"), (2, "value"), (3, "value")], errors.Select(e => (e.Action, e.Field)));
    }

    [Fact]
    public void Document_Keeps_A_Comments_Blank_Lines_And_Indentation()
    {
        var actions = new List<AplDocumentAction>
        {
            new("ice_comet", Comment: "\nopener\n\n  indented\n"),
            new("frost_bolt", Comment: "filler"),
        };
        var (written, errors) = AplDocument.Write(actions);
        Assert.Empty(errors);
        Assert.Equal("# opener\n#\n#   indented\nactions+=/ice_comet\n# filler\nactions+=/frost_bolt\n", written);

        var read = AplDocument.Parse(written! + "# trailing\n#\n# more\n");
        Assert.Equal("opener\n\n  indented", read[0].Comment);
        Assert.Equal("filler\ntrailing\n\nmore", read[1].Comment);
    }

    [Fact]
    public void Document_Write_Needs_A_List_With_Actions()
    {
        var (text, errors) = AplDocument.Write(
        [
            new AplDocumentAction("call_action_list", ActionList: "aoe"),
            new AplDocumentAction("run_action_list"),
        ]);
        Assert.Null(text);
        Assert.Equal([(0, "actionList"), (1, "actionList")], errors.Select(e => (e.Action, e.Field)));
    }

    [Theory]
    [InlineData("actions+=/run_action_list,name=missing", "APL line 1: no action list named `missing`.")]
    [InlineData("actions+=/call_action_list", "APL line 1: `call_action_list` needs `name=<list>`.")]
    public void Script_Rejects_A_Bad_List_Action(string script, string message) =>
        Assert.Equal(message, Assert.Throws<ParseException>(() => AplParser.ParseScript(script)).Message);

    [Fact]
    public void Bind_Rejects_An_Action_Naming_No_Ability_Of_The_Hero()
    {
        var (hero, _) = Fight(100);
        var error = Assert.Throws<InvalidOperationException>(() => AplParser.ParseScript("actions+=/frost_bolt\nactions+=/no_such_ability").Bind(hero));
        Assert.Equal("APL: Rime has no ability named `no_such_ability`.", error.Message);
    }

    // The action a decision casts with one enemy at full health, or null when nothing is cast. Frost Bolt has a cast
    // time; Cold Snap is instant and spends a charge.
    private static string? CastBy(string script)
    {
        var (hero, ctx) = Fight(100);
        var charges = AplParser.ParseExpression("cooldown.cold_snap.charges");
        double before = charges.EvaluateNumeric(hero, ctx);
        if (!AplParser.ParseScript(script).TryAct(hero, ctx)) return null;
        if (hero.CurrentCast != null) return AplNames.Slug(hero.CurrentCast.Ability.Name);
        ctx.RunUntil(SimTime.FromSeconds(0.5));
        return charges.EvaluateNumeric(hero, ctx) < before ? "cold_snap" : "";
    }

    [Theory]
    // run_action_list runs the named list.
    [InlineData("actions+=/run_action_list,name=st\nactions+=/frost_bolt\nactions.st+=/cold_snap", "cold_snap")]
    // A false condition skips the list action.
    [InlineData("actions+=/run_action_list,name=st,if=target.health.pct<50\nactions+=/frost_bolt\nactions.st+=/cold_snap", "frost_bolt")]
    // The actions after run_action_list do not run, even when nothing in the list is cast.
    [InlineData("actions+=/run_action_list,name=st\nactions+=/frost_bolt\nactions.st+=/cold_snap,if=target.health.pct<50", null)]
    // The action after call_action_list runs when nothing in the list is cast.
    [InlineData("actions+=/call_action_list,name=st\nactions+=/frost_bolt\nactions.st+=/cold_snap,if=target.health.pct<50", "frost_bolt")]
    [InlineData("actions+=/call_action_list,name=st\nactions+=/frost_bolt\nactions.st+=/cold_snap", "cold_snap")]
    // Lists that name each other end.
    [InlineData("actions+=/call_action_list,name=a\nactions+=/frost_bolt\nactions.a+=/call_action_list,name=b\nactions.b+=/run_action_list,name=a", "frost_bolt")]
    public void List_Actions_Move_The_Decision(string script, string? cast) =>
        Assert.Equal(cast, CastBy(script));

    [Fact]
    public void Document_Write_Reports_Each_Bad_Field()
    {
        var (text, errors) = AplDocument.Write(
        [
            new AplDocumentAction("frost_bolt", "a &"),
            new AplDocumentAction("", TargetSelection: AplTargetSelection.Max),
            new AplDocumentAction("ice comet", TargetExpression: "(x"),
            new AplDocumentAction("frost_bolt", Arguments: "line_cd=5,if=a"),
            new AplDocumentAction("frost_bolt", Arguments: "line_cd="),
        ]);
        Assert.Null(text);
        Assert.Equal(
            [(0, "condition"), (1, "ability"), (1, "targetExpression"), (2, "ability"), (2, "targetExpression"), (3, "arguments"), (4, "arguments")],
            errors.Select(e => (e.Action, e.Field)));
    }

    [Fact]
    public void Vocabulary_Repeatable_Abilities_Have_No_Cooldown_Or_Requirement()
    {
        var repeatable = AplVocabulary.For("Rime").RepeatableAbilities;
        Assert.Contains("frost_bolt", repeatable);
        Assert.DoesNotContain("cold_snap", repeatable);
        Assert.DoesNotContain("ice_comet", repeatable);
        Assert.DoesNotContain("frost_swallows", repeatable);

        var (hero, ctx) = Fight(100);
        foreach (string slug in repeatable)
        {
            Assert.True(AplNames.FindAbility(hero, slug)!.CanCast(ctx), slug);
        }
    }

    [Fact]
    public void Vocabulary_Names_Resolve_In_The_Engine()
    {
        var vocabulary = AplVocabulary.For("rime");
        Assert.Equal("Rime", vocabulary.Hero);
        var (hero, ctx) = Fight(100);
        foreach (var ability in hero.Abilities)
        {
            var name = new AplName(AplNames.Slug(ability.Name), ability.Name);
            Assert.Contains(name, vocabulary.Abilities);
            Assert.Equal(ability.Name, new AplAction { AbilityName = name.Slug }.ResolveAbility(hero)?.Name);
        }
        // Every weapon ability, whichever weapon the hero has.
        foreach (var weapon in Catalog.AllWeapons)
        {
            weapon.Equip(Catalog.CreateHero("Rime"));
            Assert.Contains(vocabulary.Abilities, a => a.Name == weapon.Ability!.Name);
            Assert.DoesNotContain(AplNames.Slug(weapon.Ability!.Name), vocabulary.RepeatableAbilities);
        }
        Assert.Contains(new AplName("winters_blessing", WintersBlessing.AbilityName), vocabulary.Abilities);
        Assert.Contains(vocabulary.Resources, r => r.Slug == "worb");
        Assert.Contains(vocabulary.Resources, r => r.Slug == "spirit");

        foreach (var resource in vocabulary.Resources) Assert.NotNull(hero.FindResource(resource.Slug));

        // Every aura is found by its slug once it is up.
        Assert.Contains(new AplName("glacial_assault", "Glacial Assault"), vocabulary.Auras);
        var aura = AuraDef.Internal("Frost's Edge Test", 10);
        hero.Auras.Apply(aura, ctx.CurrentTime);
        Assert.True(AplParser.ParseExpression($"buff.{AplNames.Slug(aura.Name)}.up").Evaluate(hero, ctx));
    }

    [Theory]
    [InlineData("winter_orbs", AplTokenKind.Resource)]
    [InlineData("resource.anima", AplTokenKind.Resource)]
    [InlineData("active_enemies", AplTokenKind.Named)]
    [InlineData("buff.glacial_assault.stack", AplTokenKind.Buff)]
    [InlineData("debuff.glacial_assault.remains", AplTokenKind.Debuff)]
    [InlineData("cooldown.winters_blessing.ready", AplTokenKind.Cooldown)]
    [InlineData("cooldown.chronoshift.up", AplTokenKind.Cooldown)]
    [InlineData("cooldown.icicles_of_anzhyr.ready", AplTokenKind.Cooldown)]
    [InlineData("buff.glacial_assault.colour", AplTokenKind.Unknown)]
    [InlineData("buff.no_such_aura.up", AplTokenKind.Unknown)]
    [InlineData("cooldown.no_such_ability.ready", AplTokenKind.Unknown)]
    [InlineData("mana", AplTokenKind.Unknown)]
    public void Vocabulary_Classifies_Properties(string path, AplTokenKind expected)
    {
        var (kind, problem) = AplVocabulary.For("Rime").Classify(new PropertyExpression(path));
        Assert.Equal(expected, kind);
        Assert.Equal(expected == AplTokenKind.Unknown, problem != null);
    }

    [Fact]
    public void Condition_Rows_Keep_Each_Join_And_Group_Parentheses()
    {
        var rows = AplCondition.Rows(AplParser.ParseExpression("a & b | c"));
        Assert.Equal([null, "and", "or"], rows.Select(r => r.Join));
        Assert.Equal(["a", "b", "c"], rows.Select(r => Assert.Single(r.Left!).Path));

        rows = AplCondition.Rows(AplParser.ParseExpression("worb<5&!(buff.x.up|buff.y.up)"));
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0] is { Join: null, Not: false });
        Assert.Equal(new AplTerm(null, Path: "worb"), Assert.Single(rows[0].Left!));
        var comparison = Assert.Single(rows[0].Comparisons!);
        Assert.Equal("<", comparison.Comparison);
        Assert.Equal(new AplTerm(null, Number: 5), Assert.Single(comparison.Terms));
        Assert.True(rows[1] is { Join: "and", Not: true, Items.Count: 2 });
        Assert.Equal(["buff.x.up", "buff.y.up"], rows[1].Items!.Select(r => Assert.Single(r.Left!).Path));
        Assert.Equal("or", rows[1].Items![1].Join);

        var negated = Assert.Single(AplCondition.Rows(AplParser.ParseExpression("!(worb>=5)")));
        Assert.True(negated is { Not: true, Comparisons.Count: 1 });
        Assert.True(Assert.Single(AplCondition.Rows(AplParser.ParseExpression("!buff.x.up"))) is { Not: true, Comparisons: null });
        Assert.Equal(-1, Assert.Single(Assert.Single(Assert.Single(AplCondition.Rows(AplParser.ParseExpression("worb>=-1"))).Comparisons!).Terms).Number);
    }

    [Fact]
    public void Condition_Rows_Keep_Arithmetic_Prefixes_And_Chained_Comparisons()
    {
        var row = Assert.Single(AplCondition.Rows(AplParser.ParseExpression("(active_enemies-active_dot.searing_blaze)>=1")));
        var inner = Assert.Single(Assert.Single(row.Left!).Items!);
        Assert.Equal([new AplTerm(null, Path: "active_enemies"), new AplTerm("-", Path: "active_dot.searing_blaze")], inner.Left!);

        row = Assert.Single(AplCondition.Rows(AplParser.ParseExpression("full_recharge_time<=gcd.max*3+talent.rolling_flames*0.4")));
        var terms = Assert.Single(row.Comparisons!).Terms;
        Assert.Equal([null, "*", "+", "*"], terms.Select(t => t.Operator));
        Assert.Equal(["gcd.max", null, "talent.rolling_flames", null], terms.Select(t => t.Path));
        Assert.Equal([null, 3, null, 0.4], terms.Select(t => t.Number));

        row = Assert.Single(AplCondition.Rows(AplParser.ParseExpression("a<b<=c")));
        Assert.Equal(["<", "<="], row.Comparisons!.Select(c => c.Comparison));

        row = Assert.Single(AplCondition.Rows(AplParser.ParseExpression("-!x+100*(y>=15)")));
        Assert.Equal("-!", row.Left![0].Prefix);
        Assert.Equal(">=", Assert.Single(Assert.Single(row.Left[2].Items!).Comparisons!).Comparison);

        row = Assert.Single(AplCondition.Rows(AplParser.ParseExpression("!!x")));
        Assert.True(row is { Not: false });
        Assert.Equal("!!", Assert.Single(row.Left!).Prefix);
    }

    [Fact]
    public void Vocabulary_Check_Spans_Tokens_And_Reports_Errors()
    {
        var vocabulary = AplVocabulary.For("Rime");
        var check = vocabulary.Check("winter_orbs>=3&(buff.x.up)");
        Assert.Null(check.Error);
        Assert.Equal(
            [
                (0, 11, AplTokenKind.Resource), (11, 2, AplTokenKind.Operator), (13, 1, AplTokenKind.Number),
                (14, 1, AplTokenKind.Operator), (15, 1, AplTokenKind.Parenthesis), (16, 9, AplTokenKind.Unknown),
                (25, 1, AplTokenKind.Parenthesis),
            ],
            check.Tokens.Select(t => (t.Start, t.Length, t.Kind)));

        var unfinished = vocabulary.Check("winter_orbs>=");
        Assert.NotNull(unfinished.Error);
        Assert.Equal(13, unfinished.ErrorStart);
        Assert.Equal(2, unfinished.Tokens.Count);

        var badCharacter = vocabulary.Check("a @ b");
        Assert.NotNull(badCharacter.Error);
        Assert.Equal(2, badCharacter.ErrorStart);
    }

    private static (Hero Hero, SimulationContext Ctx) Fight(params double[] healthPercents) =>
        Fight(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 }, healthPercents);

    private static (Hero Hero, SimulationContext Ctx) Fight(CharacterBuild build, params double[] healthPercents)
    {
        var hero = HeroImporter.Import(build);
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        for (int i = 0; i < healthPercents.Length; i++)
        {
            var enemy = ctx.AddEnemy(100 + i, i + 1, $"Mob {i + 1}", maxHealth: 1000);
            enemy.CurrentHealth = 10 * healthPercents[i];
        }
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        return (hero, ctx);
    }
}
