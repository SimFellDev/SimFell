using System.Text.Json;
using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// A traced fight (<see cref="MonteCarloSimulator.TraceFight"/>) is the fight the sim fights: the same casts as the same
/// fight rolled, each one the cast of a decision, and each step's values the values the decision read.
/// </summary>
public class AplFightTraceTests
{
    private static CharacterBuild Fixture(string hero) => JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{hero.ToLowerInvariant()}_character.json")))!;

    private static AplActionLists DefaultList(string hero) =>
        AplParser.ParseScript(DefaultActionLists.Read(hero, DefaultActionLists.Names(hero)[0])!);

    /// <summary>Each hero of the engine with a fixture build, against one and three training dummies.</summary>
    public static TheoryData<string, int> HeroFights()
    {
        var fights = new TheoryData<string, int>();
        foreach (string hero in Catalog.Heroes.Keys.Order()
                     .Where(h => File.Exists(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{h.ToLowerInvariant()}_character.json"))))
        {
            fights.Add(hero, 1);
            fights.Add(hero, 3);
        }
        return fights;
    }

    [Theory]
    [MemberData(nameof(HeroFights))]
    public void A_Traced_Fight_Casts_As_The_Rolled_Fight(string hero, int targets) =>
        AssertCastsAsRolled(new MonteCarloSimulator(Fixture(hero), DefaultList(hero)), SimTime.FromSeconds(120), targets, FightOptions.Default);

    [Theory]
    [MemberData(nameof(HeroFights))]
    public void A_Traced_Fight_With_A_Proc_Reaction_Time_Casts_As_The_Rolled_Fight(string hero, int targets) =>
        AssertCastsAsRolled(new MonteCarloSimulator(Fixture(hero), DefaultList(hero)), SimTime.FromSeconds(120), targets,
            FightOptions.Default with { ProcReactionSeconds = 0.5 });

    [Fact]
    public void A_Traced_Fight_With_Casts_During_Casts_Casts_As_The_Rolled_Fight()
    {
        var lists = AplParser.ParseScript("actions=ice_blitz,use_while_casting=1\nactions+=/wrath_of_winter,use_while_casting=1\nactions+=/frost_bolt");
        AssertCastsAsRolled(new MonteCarloSimulator(Fixture("Rime"), lists), SimTime.FromSeconds(120), 1, FightOptions.Default with { StartingSpirit = 100 });
    }

    [Fact]
    public void A_Traced_Dungeon_Fight_Casts_As_The_Rolled_Fight()
    {
        var dungeon = FellowshipSim.Core.Dungeons.DefaultDungeons.Default;
        var options = FightOptions.Default with { Dungeon = dungeon };
        AssertCastsAsRolled(new MonteCarloSimulator(Fixture("Rime"), DefaultList("Rime")), SimTime.FromSeconds(dungeon.TimeLimitSeconds), 1, options);
    }

    private static void AssertCastsAsRolled(MonteCarloSimulator simulator, SimTime duration, int targets, FightOptions options)
    {
        var rolled = simulator.RollFight(2, duration, targets, firstSeed: 5, options);
        var trace = simulator.TraceFight(2, duration, targets, firstSeed: 5, options);

        Assert.Equal(rolled.Casts, trace.Casts);
        Assert.NotEmpty(trace.Decisions);
        var decided = trace.Decisions.Where(d => d.Cast != null).Select(d => (d.Time, d.Cast)).ToList();
        var cast = trace.Casts.Select(c => (c.Time, (string?)c.Name)).ToList();
        // A cast still running as the fight ends is not in the fight's casts, which list a cast once it finishes.
        Assert.Equal(cast, decided.Take(cast.Count));
        Assert.InRange(decided.Count - cast.Count, 0, 1);
    }

    [Fact]
    public void A_Cast_Decision_Ends_On_Its_Cast_Step()
    {
        var trace = new MonteCarloSimulator(Fixture("Rime"), DefaultList("Rime")).TraceFight(0, SimTime.FromSeconds(60), 2);

        foreach (var decision in trace.Decisions.Where(d => d.Cast != null))
        {
            var last = decision.Steps[^1];
            Assert.Equal(AplStepOutcome.Cast, last.Outcome);
            Assert.Equal(decision.CastLine, last.Line);
            Assert.True(trace.Lines[last.Line].InBuild);
            Assert.All(decision.Steps.SkipLast(1), step => Assert.NotEqual(AplStepOutcome.Cast, step.Outcome));
        }
    }

    [Fact]
    public void A_Cast_Refused_During_The_Global_Cooldown_Has_The_Reason_And_The_Global_Cooldown_Left()
    {
        // Ardeos casts Incinerate during the global cooldown (use_off_gcd=1), so a decision runs while it lasts and Engulfing
        // Flames, use_off_gcd=1 too, ready and higher in the list, is refused.
        const string profile = """
            ardeos="Zig_3_1_test-haste_crit"
            primary_stat=161
            stamina=306
            crit_rating=135
            haste_rating=170
            expertise_rating=117
            spirit_rating=64
            talents=pyrophibian_frenzy/undying_flame/frog_squad/slow_burn/rolling_flames/cascading_inferno/apocalyptic_surge
            aspects=amethyst_splinters:4/emerald_judgement:1/visions_of_grandeur:3/brave_machinations:3/martial_initiative:2
            sigils=seized_opportunity:4
            protective_enchant=the_beetle:1/the_shadow:1/the_lake:1
            glyphs=the_wayfarer:4
            gems=ruby:500/amethyst:500/emerald:1200/sapphire:1200
            gem_sockets=flawless_emerald/flawless_emerald/flawless_sapphire/flawless_sapphire
            gear_gem_bonuses=ruby:5/amethyst:5/emerald:2/sapphire:2
            stat_enchants=crit_expertise:6/crit_haste:48
            stat_enchant_counts=crit_expertise:1/crit_haste:8
            relic1=orb_of_inner_seas,id=5926
            relic2=orb_of_hidden_riches,id=5921
            legendary=devouring_flame,id=5900
            weapon=ashas_chronoshift_spire

            actions+=/apocalypse
            actions+=/fire_frogs
            actions+=/fire_ball,if=full_recharge_time <= gcd.max * 3 & active_dot.fire_ball = 0
            actions+=/wildfire,use_off_gcd=1,if=cooldown.chronoshift.remains <= 20 | !cooldown.engulfing_flames.ready | !(spirit >= 83)
            actions+=/pyromania,use_off_gcd=1
            actions+=/engulfing_flames,use_off_gcd=1
            actions+=/fire_ball,if=full_recharge_time <= gcd.max * 3
            actions+=/chronoshift,if=buff.spirit_of_heroism.up & buff.spirit_of_heroism.remains <= gcd.max * 5 + 18 | spirit >= 70 | spirit <= 30 & !buff.martial_initiative.up
            actions+=/incinerate,use_off_gcd=1,if=buff.martial_initiative.up
            actions+=/infernal_wave,if=buff.cascading_inferno.stack = 12 & cinders <= 300
            actions+=/detonate,if=dot.engulfing_flames.stack >= 2
            actions+=/infernal_wave,if=buff.cascading_inferno.stack >= 4
            actions+=/detonate
            actions+=/infernal_wave
            """;
        var trace = Engine.SimChunks.NewSimulator(SimcProfile.Parse(profile))
            .TraceFight(0, SimTime.FromSeconds(300), 8, firstSeed: 67, FightOptions.Default with { LengthVariance = 0.2 });

        var refused = trace.Decisions.Where(d => d.Steps.Any(s => s.Outcome == AplStepOutcome.CastRefused)).ToList();
        Assert.Contains(refused, d => d.Cast == "Incinerate");
        Assert.All(refused, decision =>
        {
            var step = decision.Steps.First(s => s.Outcome == AplStepOutcome.CastRefused);
            Assert.StartsWith("on global cooldown", step.Refusal);
            Assert.True(decision.State.GlobalCooldown > 0);
        });
    }

    [Fact]
    public void A_Step_Shows_The_Variable_Value_The_Decision_Read()
    {
        const string apl = """
            actions=variable,name=count,op=add,value=1
            actions+=/frost_bolt,if=variable.count>=3&variable.count<=4
            actions+=/cold_snap,if=variable.count>=99
            actions+=/ice_comet,if=variable.count>=99
            """;
        var trace = new MonteCarloSimulator(Fixture("Rime"), AplParser.ParseScript(apl)).TraceFight(0, SimTime.FromSeconds(20));

        var first = trace.Decisions[0];
        Assert.Equal(AplStepOutcome.VariableSet, first.Steps[0].Outcome);
        Assert.Equal(1, first.Steps[0].Value);
        var frostBolt = first.Steps[1];
        Assert.Equal(AplStepOutcome.ConditionFalse, frostBolt.Outcome);
        Assert.Equal(0, frostBolt.Condition!.Value);
        Assert.Equal("variable.count >= 3", frostBolt.Condition.Operands[0].Text);
        Assert.Equal(1, frostBolt.Condition.Operands[0].Operands[0].Value);
        // '&' never read its right side after a false left side.
        Assert.True(frostBolt.Condition.Operands[1].Skipped);

        var third = trace.Decisions[2];
        Assert.Equal(3, third.Steps[0].Value);
        Assert.Equal("Frost Bolt", third.Cast);
        Assert.Equal(new AplVariableValue("count", 2), Assert.Single(third.State.Variables));
    }

    [Fact]
    public void A_Line_Number_Is_The_Lines_Number_In_The_Text()
    {
        const string apl = """
            # Rime

            actions+=/cold_snap,if=0

            # Filler
            actions+=/frost_bolt
            """;
        var trace = new MonteCarloSimulator(Fixture("Rime"), AplParser.ParseScript(apl)).TraceFight(0, SimTime.FromSeconds(10));

        Assert.Equal([3, 6], trace.Lines.Select(l => l.LineNumber));
        Assert.Equal("actions+=/frost_bolt", trace.Lines[1].Text);
    }

    [Fact]
    public void A_Line_Of_Another_Build_Is_Out_Of_The_Build()
    {
        // The fixture's weapon is Asha's Chronoshift Spire; Voidbringer's Touch is another weapon's ability.
        const string apl = """
            actions+=/voidbringers_touch
            actions+=/frost_bolt
            """;
        var trace = new MonteCarloSimulator(Fixture("Rime"), AplParser.ParseScript(apl)).TraceFight(0, SimTime.FromSeconds(10));

        Assert.False(trace.Lines[0].InBuild);
        Assert.Equal(2, trace.Lines[1].LineNumber);
        Assert.All(trace.Decisions, d => Assert.All(d.Steps, s => Assert.Equal(1, s.Line)));
    }
}
