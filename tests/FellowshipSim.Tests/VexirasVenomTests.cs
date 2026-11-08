using FellowshipSim.Core.Customization.Legendaries;

namespace FellowshipSim.Tests;

/// <summary>A crit on a running Vexira's Venom, with the numbers of Mara's logged refreshes.</summary>
public class VexirasVenomTests
{
    [Theory]
    // T2qLPpmMkrAtJ9B1 fight 18 @ 31.671, enemy 7.1: the next tick and those after it are in the time left.
    [InlineData(1787, 15061, 3.931, 1.401, 0.335, 2506)]
    // 3M2vWr8taxVfTQFY fight 53 @ 17.862, enemy 98.1.
    [InlineData(1321, 13122, 3.923, 1.738, 0.200, 2043)]
    // 3M2vWr8taxVfTQFY fight 53 @ 28.997, enemy 96.1: the time left ends before the next tick.
    [InlineData(2506, 20136, 0.377, 1.305, 0.472, 2006)]
    public void A_Refresh_Spreads_The_Ticks_Left_And_The_Crit_Over_The_Ticks_Of_A_New_Venom(
        double tick, double crit, double left, double nextTick, double sinceTick, double logged)
    {
        double refreshed = VexirasVenom.RefreshedTick(tick, VexirasVenom.DamageToDotDamageMultiplier * crit, left, nextTick, sinceTick);

        Assert.InRange(refreshed / logged, 0.99, 1.01);
    }
}
