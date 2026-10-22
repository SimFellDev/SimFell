using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Benchmarks;

/// <summary>
/// The Rime fight every benchmark uses: the test fixture's character (tests/FellowshipSim.Tests/Fixtures/rime_character.json)
/// with the action list in Fixtures/rime.simc, against training mobs.
/// </summary>
public static class Fight
{
    /// <summary>Length of a Monte Carlo fight.</summary>
    public static readonly SimTime Duration = SimTime.FromSeconds(300);

    /// <summary>Fight time of the hero the micro-benchmarks start from.</summary>
    public static readonly SimTime MidFight = SimTime.FromSeconds(60);

    public static CharacterBuild Build { get; } =
        JsonSerializer.Deserialize<CharacterBuild>(File.ReadAllText(FixturePath("rime_character.json")))!;

    public static string AplText { get; } = File.ReadAllText(FixturePath("rime.simc"));

    public static AplActionLists Actions() => AplParser.ParseScript(AplText);

    public static Hero NewHero() => HeroImporter.Import(Build);

    /// <summary>A new fight on <paramref name="hero"/> against <paramref name="targets"/> training mobs, set up as <see cref="MonteCarloSimulator"/> sets up each fight.</summary>
    public static SimulationContext Start(Hero hero, int targets)
    {
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        for (int i = 1; i <= targets; i++)
        {
            ctx.AddEnemy(100 + i, i, $"Training Mob {i}", maxHealth: 100_000_000);
        }
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        return ctx;
    }

    /// <summary>
    /// Runs the fight to <paramref name="until"/> with <see cref="MonteCarloSimulator"/>'s decision loop. Each pass of the
    /// loop is counted in <paramref name="counts"/> when given.
    /// </summary>
    public static void RunTo(Hero hero, SimulationContext ctx, AplActionLists actions, SimTime until, LoopCounts? counts = null)
    {
        while (ctx.CurrentTime < until)
        {
            bool free = hero.CurrentCast == null && hero.CurrentChannel == null && ctx.CurrentTime >= hero.GlobalCooldownEndTime;
            bool acted = free && actions.TryAct(hero, ctx);
            if (counts != null)
            {
                counts.Passes++;
                if (free) counts.Decisions++;
                if (free && !acted) counts.EmptyDecisions++;
            }

            var next = hero.GlobalCooldownEndTime;
            if (hero.CurrentCast != null && hero.CurrentCast.EndTime > next) next = hero.CurrentCast.EndTime;
            if (hero.CurrentChannel != null && hero.CurrentChannel.Ticks.End > next) next = hero.CurrentChannel.Ticks.End;
            if (!acted && next <= ctx.CurrentTime)
            {
                next = ctx.CurrentTime + SimTime.FromSeconds(0.1);
                if (counts != null) counts.IdlePolls++;
            }
            ctx.RunUntil(next < until ? next : until);
        }
    }

    /// <summary>A hero <see cref="MidFight"/> into a fight against <paramref name="targets"/> training mobs.</summary>
    public static (Hero Hero, SimulationContext Context, AplActionLists Actions) AtMidFight(int targets)
    {
        var hero = NewHero();
        var ctx = Start(hero, targets);
        var actions = Actions();
        RunTo(hero, ctx, actions, MidFight);
        return (hero, ctx, actions);
    }

    /// <summary>
    /// The path of a fixture: next to the program (Fixtures/), else in the repository (the benchmark's Fixtures folder or the
    /// test fixtures). BenchmarkDotNet runs the benchmarks from a build in a subfolder of the program's folder.
    /// </summary>
    private static string FixturePath(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string local = Path.Combine(dir.FullName, "Fixtures", name);
            if (File.Exists(local)) return local;
            if (File.Exists(Path.Combine(dir.FullName, "FellowshipSim.slnx")))
            {
                foreach (string path in new[]
                         {
                             Path.Combine(dir.FullName, "benchmarks", "FellowshipSim.Benchmarks", "Fixtures", name),
                             Path.Combine(dir.FullName, "tests", "FellowshipSim.Tests", "Fixtures", name),
                         })
                {
                    if (File.Exists(path)) return path;
                }
            }
        }
        throw new FileNotFoundException($"Fixture {name} not found from {AppContext.BaseDirectory}");
    }
}

/// <summary>Passes of the decision loop in one fight.</summary>
public sealed class LoopCounts
{
    /// <summary>Passes of the loop.</summary>
    public int Passes;
    /// <summary>Passes where the hero was free to act (one action-list evaluation each).</summary>
    public int Decisions;
    /// <summary>Decisions that cast nothing.</summary>
    public int EmptyDecisions;
    /// <summary>Passes that advanced the clock by the fixed 0.1 s step.</summary>
    public int IdlePolls;
}
