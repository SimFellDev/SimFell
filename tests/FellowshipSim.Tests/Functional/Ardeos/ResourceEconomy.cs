using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's Cinders and Burning Embers: his damage over time effects generate Cinders as they tick, every 100 are a Burning Ember,
/// and Detonate spends an Ember. Each test has a "Player" line (the rule) and a "Dev" line (how it is done).
/// </summary>
public class ArdeosResourceEconomy
{
    private const double Tolerance = 0.01;

    private static double Cinders(StepDriver f) => f.Hero.ResourceAmount(Ardeos.Cinders);

    /// <summary>
    /// Player: a Searing Blaze that runs its full 30 seconds generates 12 Cinders.
    /// Dev: one Searing Blaze and 40 seconds; 12 Cinders.
    /// TODO: the number of Cinders per tick is not settled: the engine has 12 over the burn (0.8 a tick), the C++ test's design 12, and 1 a tick
    /// would make 15 (1 a second, 30). To check in game.
    /// </summary>
    [Fact(Skip = "Cinders of a full Searing Blaze: 12 in the engine, 15 if 1 per tick: to check in game")]
    public void A_Full_Searing_Blaze_Generates_12_Cinders()
    {
        var f = StepFights.Ardeos();

        f.Cast<SearingBlaze>();
        f.Wait(40);

        Assert.Equal(12, Cinders(f), Tolerance);
    }

    /// <summary>
    /// Player: an Engulfing Flames that runs its full 9 seconds generates 30 Cinders.
    /// Dev: one Engulfing Flames and 20 seconds; 30 Cinders.
    /// </summary>
    [Fact]
    public void A_Full_Engulfing_Flames_Generates_30_Cinders()
    {
        var f = StepFights.Ardeos();

        f.Cast<EngulfingFlames>();
        f.Wait(20);

        Assert.Equal(30, Cinders(f), Tolerance);
    }

    /// <summary>
    /// Player: every 100 Cinders are a Burning Ember, and what is over stays as Cinders.
    /// Dev: Cinders set to 99, then 250; 0 and then 2 Embers.
    /// </summary>
    [Fact]
    public void Every_100_Cinders_Are_A_Burning_Ember()
    {
        var f = StepFights.Ardeos();

        f.Hero.Resources.Get(Ardeos.Cinders).Current = 99;
        Assert.Equal(0, f.ArdeosOf().BurningEmbers);
        f.Hero.Resources.Get(Ardeos.Cinders).Current = 250;
        Assert.Equal(2, f.ArdeosOf().BurningEmbers);
    }

    /// <summary>
    /// Player: Detonate costs a Burning Ember: you cannot cast it without one, and it uses one up (and no other Cinders).
    /// Dev: one enemy with a Searing Blaze; a Detonate with 99 Cinders is refused; with 250 it works and leaves 150.
    /// </summary>
    [Fact]
    public void Detonate_Costs_A_Burning_Ember()
    {
        var f = StepFights.Ardeos();
        f.Cast<SearingBlaze>();
        f.Hero.Resources.Get(Ardeos.Cinders).Current = 99;
        Assert.False(f.TryCast<Detonate>().Succeeded);

        f.Hero.Resources.Get(Ardeos.Cinders).Current = 250;
        f.Cast<Detonate>();

        Assert.Equal(150, Cinders(f), 1.0); // the Searing Blaze's own ticks add a little
        Assert.Equal(1, f.ArdeosOf().BurningEmbers);
    }
}
