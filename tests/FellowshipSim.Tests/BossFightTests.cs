using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Ruby's Blessing of the Conqueror counts only in a fight against a boss, which a sim's fight is unless told not.</summary>
public class BossFightTests
{
    [Fact]
    public void Without_A_Boss_The_Conqueror_Does_Not_Count()
    {
        var build = JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        build.GemPower[GemColor.Ruby] = 450;
        var simulator = new MonteCarloSimulator(build, AplParser.ParseScript("actions+=/frost_bolt"));
        double Mean(FightOptions? fight) => simulator.Run(SimTime.FromSeconds(60), iterations: 20, degreeOfParallelism: 1, fight: fight).MeanDps;

        double boss = Mean(new FightOptions(Boss: true)), noBoss = Mean(new FightOptions(Boss: false));
        Assert.Equal(boss, Mean(null));
        Assert.Equal(1.05, boss / noBoss, 2);
    }
}
