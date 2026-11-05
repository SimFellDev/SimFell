using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests;

/// <summary>Rising Spirit's duration over re-applications, at the times of two buffs in T2qLPpmMkrAtJ9B1 fight 18.</summary>
public class RisingSpiritTests
{
    private static Aura ApplyAt(params double[] seconds)
    {
        var auras = new AuraContainer();
        Aura? aura = null;
        foreach (double s in seconds) aura = auras.Apply(Hero.RisingSpirit, SimTime.FromSeconds(s));
        return aura!;
    }

    [Fact]
    public void Each_further_stack_adds_two_seconds()
    {
        var aura = ApplyAt(137.607, 138.642, 148.158);

        Assert.Equal(3, aura.Stacks);
        Assert.Equal(SimTime.FromSeconds(137.607 + 14 + AuraContainer.ExpiryLagSeconds), aura.ExpiresAt);
    }

    [Fact]
    public void The_duration_stops_at_twenty_seconds_and_a_stack_still_comes()
    {
        var aura = ApplyAt(95.261, 96.297, 102.277, 105.383, 108.488, 113.029, 114.065);

        Assert.Equal(7, aura.Stacks);
        Assert.Equal(SimTime.FromSeconds(95.261 + 20 + AuraContainer.ExpiryLagSeconds), aura.ExpiresAt);
    }
}
