using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests;

/// <summary>Rising Spirit: each further stack adds 2s to the time left, and the stacks add 10s at most.</summary>
public class RisingSpiritTests
{
    [Fact]
    public void Each_Stack_Adds_Two_Seconds_Up_To_Ten()
    {
        var auras = new AuraContainer();
        var aura = auras.Apply(Hero.RisingSpirit, SimTime.Zero);
        double lag = AuraContainer.ExpiryLagSeconds;
        Assert.Equal(Hero.RisingSpiritSeconds + lag, aura.ExpiresAt.TotalSeconds, 2);
        // Nine more stacks, one a second: 2s each until 10s are added (the fifth), then none.
        for (int i = 1; i <= 9; i++)
        {
            aura = auras.Apply(Hero.RisingSpirit, SimTime.FromSeconds(i));
            double added = Math.Min(i * Hero.RisingSpiritReapplySeconds, Hero.RisingSpiritReapplyUpToSeconds);
            Assert.Equal(Hero.RisingSpiritSeconds + added + lag, aura.ExpiresAt.TotalSeconds, 2);
        }
        Assert.Equal(Hero.RisingSpiritMaxStacks, aura.Stacks);
    }
}
