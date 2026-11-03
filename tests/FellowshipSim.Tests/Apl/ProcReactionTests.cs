using System.Text.Json;
using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Apl;

/// <summary>
/// An action list sees a proc's gain once the proc reaction time has passed. Until then it reads the proc as it was before
/// the gain: down, its stacks then, and its time left then.
/// </summary>
public class ProcReactionTests
{
    private static readonly AuraDef Proc = AuraDef.Internal("Test Proc", 10, maxStacks: 3) with { IsProc = true };
    private static readonly SimTime Reaction = SimTime.FromMilliseconds(500);

    [Fact]
    public void A_New_Proc_Is_Down_Until_The_Reaction_Time_Passes()
    {
        var (hero, ctx) = Fight();
        hero.Auras.Apply(Proc, ctx.CurrentTime);

        Assert.Equal(0.0, Read(hero, ctx, "buff.test_proc.up"));
        Assert.Equal(1.0, Read(hero, ctx, "buff.test_proc.down"));
        Assert.Equal(0.0, Read(hero, ctx, "buff.test_proc.stack"));

        ctx.RunUntil(Reaction);
        Assert.Equal(1.0, Read(hero, ctx, "buff.test_proc.up"));
        Assert.Equal(1.0, Read(hero, ctx, "buff.test_proc.stack"));
    }

    [Fact]
    public void A_Stack_Reads_The_Stacks_Before_It_Until_The_Reaction_Time_Passes()
    {
        var (hero, ctx) = Fight();
        hero.Auras.Apply(Proc, ctx.CurrentTime);
        ctx.RunUntil(SimTime.FromSeconds(7));
        hero.Auras.Apply(Proc, ctx.CurrentTime);

        Assert.Equal(1.0, Read(hero, ctx, "buff.test_proc.stack"));
        // The time left before the gain, counting down from the old expiry.
        Assert.Equal(3.0, Read(hero, ctx, "buff.test_proc.remains"), 1);

        ctx.RunUntil(SimTime.FromSeconds(7) + Reaction);
        Assert.Equal(2.0, Read(hero, ctx, "buff.test_proc.stack"));
        Assert.Equal(9.5, Read(hero, ctx, "buff.test_proc.remains"), 1);
    }

    [Fact]
    public void An_Aura_That_Is_No_Proc_Is_Seen_At_Once()
    {
        var (hero, ctx) = Fight();
        hero.Auras.Apply(Proc with { IsProc = false }, ctx.CurrentTime);

        Assert.Equal(1.0, Read(hero, ctx, "buff.test_proc.up"));
    }

    [Fact]
    public void The_Next_Decision_Is_When_The_Reaction_Time_Passes()
    {
        var (hero, ctx) = Fight();
        hero.Auras.Apply(Proc, ctx.CurrentTime);
        var program = AplParser.ParseScript("actions=frost_bolt,if=buff.test_proc.up").Bind(hero);

        Assert.False(program.TryAct(ctx));
        Assert.Equal(Reaction, program.NextChange(ctx));
    }

    [Fact]
    public void A_Debugged_Decision_Refuses_The_Cast_Of_A_Proc_Inside_The_Reaction_Time()
    {
        // Tariq's default list reads the Executioner's Grin proc of the fixture build's legendary: in fight 5 of this run, a
        // decision falls inside the proc reaction time of its gain.
        var build = JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tariq_character.json")))!;
        var lists = AplParser.ParseScript(DefaultActionLists.Read("Tariq", DefaultActionLists.DefaultName)!);
        var trace = new MonteCarloSimulator(build, lists)
            .TraceFight(5, SimTime.FromSeconds(180), options: new FightOptions(LengthVariance: 0.2, HealthFollowsTime: true, ProcReactionSeconds: 0.5));

        var refused = trace.Decisions.SelectMany(d => d.Steps)
            .Where(s => s.Outcome == AplStepOutcome.CastRefused && s.Refusal!.Contains("proc reaction time")).ToList();
        Assert.NotEmpty(refused);
        Assert.All(refused, step => Assert.StartsWith("Executioner's Grin was gained", step.Refusal));
    }

    private static double Read(Hero hero, SimulationContext ctx, string expression) =>
        AplParser.ParseExpression(expression).EvaluateNumeric(hero, ctx);

    private static (Hero Hero, SimulationContext Ctx) Fight()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero) { ProcReactionTime = Reaction };
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        ctx.AddEnemy(100, 1, "Mob", maxHealth: 1000);
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        return (hero, ctx);
    }
}
