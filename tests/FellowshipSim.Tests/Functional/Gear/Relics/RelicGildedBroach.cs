using FellowshipSim.Core.Customization.RelicPassives;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gilded Broach, a relic's passive. Worn by a bare hero. The effect runs over every hero, since a relic works through what the hero casts,
/// not through named abilities; its details run on Rime. Every resource is filled before a cast, so any ability can be cast. Procs are
/// forced.
/// </summary>
public class RelicGildedBroach
{
    /// <summary>
    /// Gilded Broach gives 5% more main stat.
    ///
    /// Per the user: the tooltip only states the heal below 20% health; the game data's effect 3274 multiplies main stat by 1.05.
    /// </summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Gilded_Broach_Gives_5_Percent_Main_Stat(string hero)
    {
        var plainScenario = StepFights.Of(hero);
        var broachScenario = StepFights.Of(hero, relics: [GildedBroach.RelicItemId]);

        AssertRatio(1.05, broachScenario.MainStat() / plainScenario.MainStat());
    }
}
