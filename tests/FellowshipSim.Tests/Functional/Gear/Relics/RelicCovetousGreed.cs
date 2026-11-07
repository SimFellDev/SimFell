using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.RelicPassives;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Covetous Greed, a relic's passive. Worn by a bare hero. The effect runs over every hero, since a relic works through what the hero
/// casts, not through named abilities; its details run on Rime. Every resource is filled before a cast, so any ability can be cast. Procs
/// are forced.
/// </summary>
public class RelicCovetousGreed
{
    /// <summary>
    /// Covetous Greed raises your gem power by 25%, rounded down, in either relic slot: 1000 becomes 1250, and 350 becomes 437.
    ///
    /// The rounding is from combat logs (350 Topaz power is 437).
    /// </summary>
    [Theory]
    [InlineData("Rime", 1, 1000.0)]
    [InlineData("Ardeos", 1, 1000.0)]
    [InlineData("Elarion", 1, 1000.0)]
    [InlineData("Gunde", 1, 1000.0)]
    [InlineData("Mara", 1, 1000.0)]
    [InlineData("Tariq", 1, 1000.0)]
    [InlineData("Rime", 2, 1000.0)]
    [InlineData("Rime", 1, 350.0)]
    public void Covetous_Greed_Raises_Gem_Power_By_25_Percent_Rounded_Down(string hero, int slot, double power)
    {
        int[] relics = slot == 1 ? [CovetousGreed.OrbItemId] : [0, CovetousGreed.OrbItemId];
        var f = StepFights.Of(hero, relics: relics, gemPower: new() { [GemColor.Ruby] = power });

        Assert.Equal(Math.Floor(power * 1.25), f.GemPower(GemColor.Ruby));
    }
}
