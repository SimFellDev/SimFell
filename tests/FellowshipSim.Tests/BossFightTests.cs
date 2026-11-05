using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Ruby's Blessing of the Conqueror counts only in a fight against a boss: training dummies that are bosses, which they are
/// unless told not, or a dungeon's boss in combat. dungeon_event.boss.exists reads 1 only then.
/// </summary>
public class BossFightTests
{
    private static CharacterBuild Build() => JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;

    [Fact]
    public void Without_A_Boss_The_Conqueror_Does_Not_Count()
    {
        var build = Build();
        build.GemPower[GemColor.Ruby] = 450;
        var simulator = new MonteCarloSimulator(build, AplParser.ParseScript("actions+=/frost_bolt"));
        double Mean(FightOptions? fight) => simulator.Run(SimTime.FromSeconds(60), iterations: 20, degreeOfParallelism: 1, fight: fight).MeanDps;

        double boss = Mean(new FightOptions(Boss: true)), noBoss = Mean(new FightOptions(Boss: false));
        Assert.Equal(boss, Mean(null));
        Assert.Equal(1.05, boss / noBoss, 2);

        // Health follows a 60 s pull, so each pull lasts as long as the training dummies.
        double DungeonOf(EnemyGroup group) => Mean(new FightOptions(Dungeon: new DungeonProfile("One", 60,
            [new DungeonPull("1", [group], 60)])));
        Assert.Equal(1.05, DungeonOf(new EnemyGroup(1, "Boss", HitPoints: 1000, Boss: true)) / DungeonOf(new EnemyGroup(1, "Trash Mob", HitPoints: 1000)), 2);
    }

    [Fact]
    public void A_Boss_Fight_Runs_Its_Own_Actions()
    {
        var simulator = new MonteCarloSimulator(Build(), AplParser.ParseScript("actions+=/frost_bolt,if=dungeon_event.boss.exists"));
        double Mean(FightOptions fight) => simulator.Run(SimTime.FromSeconds(60), iterations: 5, degreeOfParallelism: 1, fight: fight).MeanDps;

        Assert.True(Mean(new FightOptions()) > 0);
        Assert.Equal(0, Mean(new FightOptions(Boss: false)));
    }
}
