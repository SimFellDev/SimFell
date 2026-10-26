using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// Where a colour's gem power comes from: the gems in the gear's sockets (24 gems in four sizes), the bonuses gear rolls (100 each), and the relics'
/// multiplier (Covetous Greed, x1.25), rounded down.
/// Dev: <see cref="GemItem"/> alone, with no hero: the numbers are written out. Covetous Greed's effect on a build is tested with the relics.
/// </summary>
public class GemSources
{
    /// <summary>
    /// Player: there are 24 gems, one of each colour in each of four sizes: Small 150, Large 250, Splendid 350 and Flawless 500 gem power.
    /// Dev: every gem's ID, name, colour and power as the game's data lists them.
    /// </summary>
    [Theory]
    [InlineData(22, "Small Amethyst", GemColor.Amethyst, 150)]
    [InlineData(27, "Small Diamond", GemColor.Diamond, 150)]
    [InlineData(32, "Small Emerald", GemColor.Emerald, 150)]
    [InlineData(38, "Small Ruby", GemColor.Ruby, 150)]
    [InlineData(43, "Small Sapphire", GemColor.Sapphire, 150)]
    [InlineData(48, "Small Topaz", GemColor.Topaz, 150)]
    [InlineData(23, "Large Amethyst", GemColor.Amethyst, 250)]
    [InlineData(28, "Large Diamond", GemColor.Diamond, 250)]
    [InlineData(33, "Large Emerald", GemColor.Emerald, 250)]
    [InlineData(39, "Large Ruby", GemColor.Ruby, 250)]
    [InlineData(44, "Large Sapphire", GemColor.Sapphire, 250)]
    [InlineData(49, "Large Topaz", GemColor.Topaz, 250)]
    [InlineData(24, "Splendid Amethyst", GemColor.Amethyst, 350)]
    [InlineData(29, "Splendid Diamond", GemColor.Diamond, 350)]
    [InlineData(34, "Splendid Emerald", GemColor.Emerald, 350)]
    [InlineData(40, "Splendid Ruby", GemColor.Ruby, 350)]
    [InlineData(45, "Splendid Sapphire", GemColor.Sapphire, 350)]
    [InlineData(50, "Splendid Topaz", GemColor.Topaz, 350)]
    [InlineData(25, "Flawless Amethyst", GemColor.Amethyst, 500)]
    [InlineData(30, "Flawless Diamond", GemColor.Diamond, 500)]
    [InlineData(35, "Flawless Emerald", GemColor.Emerald, 500)]
    [InlineData(41, "Flawless Ruby", GemColor.Ruby, 500)]
    [InlineData(46, "Flawless Sapphire", GemColor.Sapphire, 500)]
    [InlineData(51, "Flawless Topaz", GemColor.Topaz, 500)]
    public void Each_Gem_Has_Its_Colour_And_Power(int id, string name, GemColor color, int power)
    {
        var gem = GemItem.Find(id);
        Assert.NotNull(gem);
        Assert.Equal(name, gem.Name);
        Assert.Equal(color, gem.Color);
        Assert.Equal(power, gem.Power);
        Assert.Equal(24, GemItem.All.Count);
    }

    /// <summary>
    /// Player: a gear piece's gem bonus is 100 gem power; a hero has 4 sockets.
    /// Dev: the constants against the game's numbers.
    /// </summary>
    [Fact]
    public void Sockets_And_Gear_Bonuses()
    {
        Assert.Equal(4, GemItem.Sockets);
        Assert.Equal(100, GemItem.GearBonusPower);
    }

    /// <summary>
    /// Player: a colour's gem power is its socketed gems plus 100 for each gear bonus of the colour, times the relics' multiplier, rounded down;
    /// colours with nothing are not listed.
    /// Dev: a Flawless and a Small Amethyst with 5 bonuses (500 + 150 + 500); with Covetous Greed's 1.25 (1,437.5 rounds to 1,437); the logged case
    /// (a Flawless Amethyst and 7 bonuses, 1,200 x 1.25 = 1,500); a rounding case (250 x 1.25 = 312.5); a gem whose ID does not exist; two colours.
    /// </summary>
    [Fact]
    public void Gem_Power_Is_Sockets_Plus_Bonuses_Times_The_Multiplier()
    {
        var none = new Dictionary<GemColor, int>();
        Assert.Equal(1150.0, GemItem.PowerByColor([25, 22], new Dictionary<GemColor, int> { [GemColor.Amethyst] = 5 }, 1.0)[GemColor.Amethyst]);
        Assert.Equal(1437.0, GemItem.PowerByColor([25, 22], new Dictionary<GemColor, int> { [GemColor.Amethyst] = 5 }, 1.25)[GemColor.Amethyst]);
        Assert.Equal(1500.0, GemItem.PowerByColor([25], new Dictionary<GemColor, int> { [GemColor.Amethyst] = 7 }, 1.25)[GemColor.Amethyst]);
        Assert.Equal(312.0, GemItem.PowerByColor([23], new Dictionary<GemColor, int> { [GemColor.Ruby] = 0 }, 1.25)[GemColor.Amethyst]);
        Assert.Empty(GemItem.PowerByColor([9999], none, 1.0));
        var two = GemItem.PowerByColor([38, 46], new Dictionary<GemColor, int> { [GemColor.Ruby] = 2 }, 1.0);
        Assert.Equal(2, two.Count);
        Assert.Equal(350.0, two[GemColor.Ruby]);
        Assert.Equal(500.0, two[GemColor.Sapphire]);
        Assert.DoesNotContain(GemColor.Topaz, two.Keys);
    }
}
