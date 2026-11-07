using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gem power over 1500 in a colour unlocks no more nodes, but each point over it gives 0.005% more main stat, summed over the colours.
/// Amethyst and Topaz are the colours, since none of their nodes changes main stat.
/// </summary>
public class GemOvercap
{
    /// <summary>Each point of gem power over 1500 gives you 0.005% more main stat: 100 points over, in all colours, give 0.5%.</summary>
    [Theory]
    [InlineData(1600, 1500, 1.005)]
    [InlineData(1700, 1500, 1.01)]
    [InlineData(1600, 1600, 1.01)]
    public void Gem_Power_Over_1500_Gives_0_005_Percent_Main_Stat_Per_Point(double amethyst, double topaz, double ratio)
    {
        var atCapScenario = StepFights.Of("Rime", gemPower: new() { [GemColor.Amethyst] = 1500, [GemColor.Topaz] = 1500 });
        var overCapScenario = StepFights.Of("Rime", gemPower: new() { [GemColor.Amethyst] = amethyst, [GemColor.Topaz] = topaz });

        AssertRatio(ratio, overCapScenario.MainStat() / atCapScenario.MainStat());
    }
}
