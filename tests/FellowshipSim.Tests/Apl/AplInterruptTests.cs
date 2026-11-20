using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Apl;

/// <summary>
/// <c>interrupt_if=&lt;expr&gt;</c> stops the channel its action started: right after a tick once the global cooldown has
/// ended, or with <c>interrupt_immediate=1</c> the moment the expression is true. The expression reads the channel's
/// <c>ticks</c>, <c>after_sec</c> and <c>after_pct</c>. As in SimulationCraft, the channel stops only for a ready action
/// above its line.
/// </summary>
public class AplInterruptTests
{
    // Frost Bolt above the channel, ready once the channel's cast has started.
    private const string Above = "actions=frost_bolt,if=prev.freezing_torrent\n";
    private const string Rest = "\nactions+=/frost_bolt";

    [Fact]
    public void A_Channel_With_No_Ready_Action_Above_Its_Line_Runs_To_Its_End()
    {
        var full = Fight("actions=freezing_torrent" + Rest);
        var fight = Fight("actions=freezing_torrent,interrupt_if=ticks>=2,interrupt_immediate=1" + Rest);

        Assert.Equal(Ticks(full), Ticks(fight));
        Assert.Equal(FirstFrostBolt(full), FirstFrostBolt(fight));
    }

    [Fact]
    public void An_Action_Above_The_Channel_Whose_Condition_Is_False_Does_Not_Interrupt_It()
    {
        var full = Fight(Above + "actions+=/freezing_torrent" + Rest);
        var fight = Fight("actions=frost_bolt,if=prev.frost_bolt\nactions+=/freezing_torrent,interrupt_if=ticks>=2,interrupt_immediate=1" + Rest);

        Assert.Equal(Ticks(full), Ticks(fight));
    }

    [Fact]
    public void The_Interrupt_Is_Read_From_The_Action_Line_With_The_Channel_Values_Of_Its_Ability()
    {
        var action = AplParser.ParseActionLine("actions=freezing_torrent,interrupt_if=ticks>=2&after_sec<1.5,interrupt_immediate=1")!;

        Assert.Equal("channel.freezing_torrent.ticks >= 2 & channel.freezing_torrent.after_sec < 1.5", AplFormatter.Format(action.InterruptIf!));
        Assert.True(action.InterruptImmediate);
        Assert.Null(action.Condition);
    }

    [Fact]
    public void An_Action_Without_Interrupt_If_Has_None()
    {
        var action = AplParser.ParseActionLine("actions=freezing_torrent,interrupt_immediate=0")!;

        Assert.Null(action.InterruptIf);
        Assert.False(action.InterruptImmediate);
    }

    [Fact]
    public void Interrupt_Immediate_Stops_The_Channel_At_The_Tick_That_Makes_The_Condition_True()
    {
        var fight = Fight(Above + "actions+=/freezing_torrent,interrupt_if=ticks>=2,interrupt_immediate=1" + Rest);

        Assert.Equal(2, Ticks(fight).Count);
    }

    [Fact]
    public void Interrupt_If_Stops_The_Channel_At_The_First_Tick_After_The_Global_Cooldown()
    {
        var full = Fight(Above + "actions+=/freezing_torrent" + Rest);
        var fight = Fight(Above + "actions+=/freezing_torrent,interrupt_if=ticks>=2" + Rest);

        var ticks = Ticks(fight);
        double frostBolt = FirstFrostBolt(fight);
        Assert.InRange(ticks.Count, 2, Ticks(full).Count - 1);
        // The cast after the channel starts at its last tick, with no reaction to the channel's end.
        Assert.Equal(ticks[^1], frostBolt, 3);
        Assert.True(frostBolt < FirstFrostBolt(full));
    }

    [Fact]
    public void The_Cast_After_An_Interrupt_Has_No_Reaction_To_The_Channel_End_It_Cut()
    {
        var reacting = new FightOptions(ReactionDelays: true);
        var full = Fight(Above + "actions+=/freezing_torrent" + Rest, reacting);
        var fight = Fight(Above + "actions+=/freezing_torrent,interrupt_if=ticks>=2" + Rest, reacting);

        var ticks = Ticks(fight);
        Assert.True(ticks.Count < Ticks(full).Count);
        Assert.Equal(ticks[^1], FirstFrostBolt(fight), 3);
        // Without the interrupt, the cast waits for the channel's end and a reaction after it.
        Assert.True(FirstFrostBolt(full) > Ticks(full)[^1]);
    }

    [Fact]
    public void After_Sec_Stops_The_Channel_That_Many_Seconds_After_It_Started()
    {
        var full = Fight(Above + "actions+=/freezing_torrent" + Rest);
        var fight = Fight(Above + "actions+=/freezing_torrent,interrupt_if=after_sec>=1,interrupt_immediate=1" + Rest);

        Assert.Contains(Ticks(full), t => t > 1.0);
        Assert.All(Ticks(fight), t => Assert.True(t <= 1.0 + 1e-9));
        Assert.True(FirstFrostBolt(fight) < FirstFrostBolt(full));
    }

    [Fact]
    public void After_Pct_Stops_The_Channel_When_That_Part_Of_Its_Duration_Has_Gone()
    {
        var full = Fight(Above + "actions+=/freezing_torrent" + Rest);
        var fight = Fight(Above + "actions+=/freezing_torrent,interrupt_if=after_pct>=50,interrupt_immediate=1" + Rest);

        // The full channel's last tick comes at its end.
        double half = Ticks(full)[^1] / 2;
        Assert.Contains(Ticks(full), t => t > half);
        Assert.All(Ticks(fight), t => Assert.True(t <= half + 1e-3));
    }

    [Theory]
    [InlineData(PropertyExpression.ChannelTicksProperty, 0.0)]
    [InlineData(PropertyExpression.ChannelSecondsProperty, 0.0)]
    [InlineData(PropertyExpression.ChannelPercentProperty, 0.0)]
    public void A_Channel_Value_Reads_0_While_The_Channel_Does_Not_Run(string property, double expected)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var path = new PropertyExpression($"{PropertyExpression.ChannelScope}.freezing_torrent.{property}");

        Assert.Equal(expected, path.EvaluateNumeric(hero, ctx));
        Assert.Equal(expected, BoundExpression.Bind(path, hero).EvaluateNumeric(ctx));
    }

    [Fact]
    public void The_Bound_And_Unbound_Channel_Values_Agree_While_The_Channel_Runs()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Mob", maxHealth: 1e9);
        hero.CurrentTarget = enemy;
        Assert.True(ctx.BeginCast(hero, hero.GetAbility<FreezingTorrent>()!, enemy).Succeeded);
        ctx.RunUntil(SimTime.FromSeconds(0.9), null);

        var channel = hero.CurrentChannel!;
        foreach (string property in PropertyExpression.Scopes[PropertyExpression.ChannelScope])
        {
            var path = new PropertyExpression($"{PropertyExpression.ChannelScope}.freezing_torrent.{property}");
            Assert.Equal(path.EvaluateNumeric(hero, ctx), BoundExpression.Bind(path, hero).EvaluateNumeric(ctx), 9);
        }
        Assert.Equal(channel.Ticks.TicksFired, new PropertyExpression("channel.freezing_torrent.ticks").EvaluateNumeric(hero, ctx));
        Assert.Equal(0.9, new PropertyExpression("channel.freezing_torrent.after_sec").EvaluateNumeric(hero, ctx), 9);
        double duration = (channel.Ticks.End - channel.Ticks.Start).TotalSeconds;
        Assert.Equal(90.0 / duration, new PropertyExpression("channel.freezing_torrent.after_pct").EvaluateNumeric(hero, ctx), 9);
    }

    [Fact]
    public void The_Check_Marks_An_Interrupt_On_An_Ability_That_Does_Not_Channel()
    {
        var check = AplVocabulary.For("Rime").CheckList("actions=frost_bolt,interrupt_if=ticks>=1\nactions+=/freezing_torrent,interrupt_if=ticks>=1");

        var problems = check.Tokens.Where(t => t.Problem != null).Select(t => t.Problem!).ToList();
        Assert.Equal(2, problems.Count);
        Assert.All(problems, p => Assert.StartsWith("Line 1: `frost_bolt` is not a channel", p));
    }

    [Fact]
    public void The_Vocabulary_Has_The_Channels_Of_The_Hero()
    {
        var v = AplVocabulary.For("Rime");

        Assert.Contains(v.Channels, c => c.Slug == "freezing_torrent");
        Assert.DoesNotContain(v.Channels, c => c.Slug == "frost_bolt");
        Assert.Contains("freezing_torrent", v.TickingChannels);
    }

    [Fact]
    public void The_Editor_Holds_The_Interrupt_In_Fields_Of_Its_Own()
    {
        var actions = AplDocument.Parse("actions=freezing_torrent,interrupt_if=ticks>=2&!buff.chill.up,interrupt_immediate=1,line_cd=5");

        Assert.Equal(new AplDocumentAction("freezing_torrent", Arguments: "line_cd=5", InterruptIf: "ticks >= 2 and not buff.chill.up", InterruptImmediate: true),
            actions[0]);
        var (text, errors) = AplDocument.Write(actions);
        Assert.Empty(errors);
        Assert.Equal("actions+=/freezing_torrent,line_cd=5,interrupt_if=ticks >= 2 & !buff.chill.up,interrupt_immediate=1\n", text);
    }

    // A 5 s fight of the Rime fixture build.
    private static RolledFight Fight(string apl, FightOptions? options = null)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        return new MonteCarloSimulator(build, AplParser.ParseScript(apl)).RollFight(0, SimTime.FromSeconds(5), options: options);
    }

    // The times of the first channel's Freezing Torrent hits.
    private static List<double> Ticks(RolledFight fight) =>
        fight.Hits.Where(h => h.Name == FreezingTorrent.AbilityName && h.Time < FirstFrostBolt(fight) + 1e-9).Select(h => h.Time).ToList();

    private static double FirstFrostBolt(RolledFight fight) => fight.Casts.First(c => c.Name == FrostBolt.AbilityName).Time;
}
