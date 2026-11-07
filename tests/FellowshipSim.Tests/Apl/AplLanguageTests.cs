using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Apl;

/// <summary>
/// The SimulationCraft expressions and actions the old Rime action lists use: build parts, variables, fight values, and
/// actions for abilities another build has.
/// </summary>
public class AplLanguageTests
{
    [Theory]
    [InlineData("talent.icy_talons", true, 1.0)]
    [InlineData("talent.icy_talons", false, 0.0)]
    [InlineData("!talent.icy_talons", false, 1.0)]
    [InlineData("3+talent.icy_talons", true, 4.0)]
    [InlineData("blessing.the_vehement", true, 0.0)]
    [InlineData("legendary.frostwyrms_spite", true, 0.0)]
    [InlineData("weapon.voidbringer", true, 0.0)]
    [InlineData("weapon_trait.visions_of_grandeur", true, 0.0)]
    [InlineData("dungeon_event.adds.exists", true, 0.0)]
    [InlineData("dungeon_event.adds.in>=12", true, 1.0)]
    [InlineData("raid_event.adds.exists", true, 0.0)]
    [InlineData("raid_event.adds.in>=12", true, 1.0)]
    public void Build_Parts_And_Dungeon_Events_Read_The_Same_Bound_And_Interpreted(string text, bool icyTalons, double expected)
    {
        var (hero, ctx) = Fight(icyTalons ? [IcyTalons.TalentId] : []);
        var expression = AplParser.ParseExpression(text);
        Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx));
        Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx));
    }

    [Theory]
    [InlineData("dungeon_event.boss.exists", false, false, 0.0)]
    [InlineData("raid_event.boss.exists", true, false, 1.0)]
    [InlineData("raid_event.boss.final", true, false, 0.0)]
    [InlineData("dungeon_event.boss.final", true, true, 1.0)]
    [InlineData("dungeon_event.boss.exists&!dungeon_event.boss.final", true, false, 1.0)]
    [InlineData("dungeon_event.boss.exists&!dungeon_event.boss.final", true, true, 0.0)]
    [InlineData("dungeon_event.boss.exists&!dungeon_event.boss.final", false, false, 0.0)]
    public void Boss_Conditions_Read_The_Fight(string text, bool boss, bool finalBoss, double expected)
    {
        var (hero, ctx) = Fight([]);
        (ctx.Boss, ctx.FinalBoss) = (boss, finalBoss);
        var expression = AplParser.ParseExpression(text);
        Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx));
        Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx));
    }

    [Fact]
    public void Gcd_Deficit_And_Execute_Time_Read_The_Hero()
    {
        var (hero, ctx) = Fight([]);
        double haste = hero.GetHaste(ctx.CurrentTime);
        double gcd = hero.GlobalCooldownSeconds / (1.0 + haste);
        var spirit = hero.Resources.Get(hero.FindResource("spirit")!);
        var frostBolt = AplNames.FindAbility(hero, "frost_bolt")!;

        foreach (var (text, expected) in new[]
                 {
                     ("gcd.max", gcd),
                     ("gcd", gcd),
                     ("spirit.deficit", spirit.Max - spirit.Current),
                     ("action.frost_bolt.execute_time", frostBolt.GetAdjustedCastTime(ctx.CurrentTime).TotalSeconds),
                     ("action.cold_snap.execute_time", gcd),
                     ("buff.no_such_buff.react", 0.0),
                 })
        {
            var expression = AplParser.ParseExpression(text);
            Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx), 9);
            Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx), 9);
        }
    }

    [Theory]
    [InlineData("actions=bursting_ice,if=refreshable", "debuff.bursting_ice.refreshable")]
    [InlineData("actions=cold_snap,if=full_recharge_time<=gcd.max*3", "(cooldown.cold_snap.full_recharge_time <= (gcd.max * 3))")]
    [InlineData("actions=frost_bolt,if=execute_time<1", "(action.frost_bolt.execute_time < 1)")]
    public void Paths_About_The_Action_Read_Its_Own_Ability(string line, string condition)
    {
        Assert.Equal(condition, AplParser.ParseActionLine(line)!.Condition!.ToString());
    }

    [Fact]
    public void A_Debuff_Is_Refreshable_When_Down_And_In_The_Last_30_Percent()
    {
        var (hero, ctx) = Fight([]);
        var refreshable = BoundExpression.Bind(AplParser.ParseExpression("debuff.bursting_ice.refreshable"), hero);
        Assert.True(refreshable.Evaluate(ctx));

        var debuff = Core.Heroes.Rime.Abilities.BurstingIce.Debuff;
        ctx.Player.CurrentTarget.Auras.Apply(debuff, ctx.CurrentTime);
        Assert.False(refreshable.Evaluate(ctx));
        // 3s debuff: refreshable once under 0.9s remain.
        var expires = ctx.Player.CurrentTarget.Auras.FindActive("bursting_ice", ctx.CurrentTime)!.ExpiresAt;
        Assert.Equal(SimTime.FromMilliseconds(expires.Milliseconds - 899), refreshable.NextChange(ctx));
    }

    [Theory]
    [InlineData("actions=variable,name=go,value=active_enemies>=1\nactions+=/frost_bolt,if=variable.go", true)]
    [InlineData("actions=variable,name=go,value=active_enemies>=2\nactions+=/frost_bolt,if=variable.go", false)]
    [InlineData("actions=variable,name=go,value=2\nactions+=/variable,name=go,value=1,op=sub\nactions+=/frost_bolt,if=variable.go=1", true)]
    [InlineData("actions=variable,name=go,value=1,if=talent.icy_talons\nactions+=/frost_bolt,if=variable.go", false)]
    [InlineData("actions+=/frost_bolt,if=variable.never_set", false)]
    public void Variables_Are_Set_In_The_Decision_Before_They_Are_Read(string script, bool casts)
    {
        var (hero, ctx) = Fight([]);
        Assert.Equal(casts, AplParser.ParseScript(script).TryAct(hero, ctx));
    }

    [Fact]
    public void A_Variable_Starts_Each_Fight_At_Its_Default()
    {
        var lists = AplParser.ParseScript("actions=frost_bolt,if=variable.go\nactions+=/variable,name=go,value=1,default=0");
        var (hero, ctx) = Fight([]);
        var program = lists.Bind(hero);
        // The first decision sets the variable after the cast it guards; the second reads it.
        Assert.False(program.TryAct(ctx));
        Assert.True(program.TryAct(ctx));

        var next = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(next);
        next.AddEnemy(100, 1, "Mob", maxHealth: 1000);
        next.Player.CurrentTarget = next.AllEnemies.First();
        Assert.False(program.TryAct(next));
    }

    [Fact]
    public void An_Action_For_Another_Weapons_Ability_Is_Left_Out()
    {
        var (hero, ctx) = Fight([]);
        var lists = AplParser.ParseScript("actions=icicles_of_anzhyr\nactions+=/frost_bolt");
        Assert.Single(lists.Bind(hero).Actions);
        Assert.True(lists.TryAct(hero, ctx));
    }

    [Fact]
    public void Every_Default_List_Binds_And_Casts()
    {
        Assert.Equal(DefaultActionLists.DefaultName, DefaultActionLists.Names("Rime")[0]);
        Assert.Equal(["Default", "Frostweaver", "Generic", "Soulfrost", "Talons"], DefaultActionLists.Names("rime"));
        foreach (string name in DefaultActionLists.Names("Rime"))
        {
            var (hero, ctx) = Fight([IcyTalons.TalentId]);
            Assert.True(AplParser.ParseScript(DefaultActionLists.Read("Rime", name)!).TryAct(hero, ctx), name);
        }
        Assert.Null(DefaultActionLists.Read("Rime", "No such list"));
    }

    [Theory]
    [InlineData("Gunde")]
    [InlineData("Ardeos")]
    [InlineData("Tariq")]
    [InlineData("Mara")]
    [InlineData("Elarion")]
    [InlineData("Xavian")]
    [InlineData("Meiko")]
    public void Each_Heros_Lists_Bind_And_Cast(string heroName)
    {
        var names = DefaultActionLists.Names(heroName);
        Assert.Equal(DefaultActionLists.DefaultName, names[0]);
        foreach (string name in names)
        {
            var (hero, ctx) = Fight([], heroName);
            Assert.True(AplParser.ParseScript(DefaultActionLists.Read(heroName, name)!).TryAct(hero, ctx), $"{heroName} {name}");
        }
    }

    [Theory]
    [InlineData("Rime")]
    [InlineData("Gunde")]
    [InlineData("Ardeos")]
    [InlineData("Tariq")]
    [InlineData("Mara")]
    [InlineData("Elarion")]
    [InlineData("Xavian")]
    [InlineData("Meiko")]
    public void Every_Aura_Gear_Gives_Is_In_The_Vocabulary(string heroName)
    {
        var auras = AplVocabulary.For(heroName).Auras.Select(a => a.Slug).ToHashSet();
        var hero = HeroImporter.Import(new CharacterBuild { Hero = heroName, PrimaryStat = 1000 });
        foreach (var glyph in Catalog.AllGlyphs) glyph.Bind(hero);
        foreach (var augmentation in Catalog.AllAugmentations) augmentation.Bind(hero);
        foreach (var relic in Catalog.AllRelicPassives) relic.Bind(hero);
        foreach (var legendary in Catalog.Legendaries(heroName)) legendary.Bind(hero);
        Assert.Empty(hero.RuntimeAuras.Select(a => AplNames.Slug(a.Name)).Where(slug => !auras.Contains(slug)).Distinct());
    }

    private static (Hero Hero, SimulationContext Ctx) Fight(IEnumerable<int> talents, string heroName = "Rime")
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = heroName, PrimaryStat = 1000, Talents = talents.ToList() });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        ctx.AddEnemy(100, 1, "Mob", maxHealth: 1000);
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        return (hero, ctx);
    }
}
