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
    /// Player: a Searing Blaze that runs its full 30 seconds generates 15 Cinders: 1 for each of its 15 ticks.
    /// Dev: one Searing Blaze and 40 seconds; 15 Cinders. (The tooltip says 12, which is outdated.)
    /// </summary>
    [Fact]
    public void A_Full_Searing_Blaze_Generates_15_Cinders()
    {
        var f = StepFights.Ardeos();

        f.Cast<SearingBlaze>();
        f.Wait(40);

        Assert.Equal(15, Cinders(f), Tolerance);
    }

    /// <summary>
    /// Player: Cinders cannot be counted in game, Burning Embers can: six Searing Blazes that tick out in full do not make an Ember
    /// (90 Cinders), seven do (105).
    /// Dev: six, then seven, Searing Blazes each on its own enemy (a recast on the same enemy only refreshes the one burn), and 40
    /// seconds for them all to tick out; 0 Embers with 6, 1 with 7.
    /// </summary>
    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    public void Searing_Blazes_That_Tick_Out_Make_An_Ember_From_The_Seventh(int blazes, int embers)
    {
        var f = StepFights.Ardeos(enemies: blazes);

        for (int i = 0; i < blazes; i++) f.Cast<SearingBlaze>(f.Context.AllEnemies[i]);
        f.Wait(40);

        Assert.Equal(embers, f.ArdeosOf().BurningEmbers);
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

    // ---- Cinders per tick, with haste and Wildfire

    /// <summary>
    /// Player: haste makes Searing Blaze tick more often but each tick gives less, so a full burn is still about 15 Cinders.
    /// Dev: one Searing Blaze and 40 seconds at each haste; the Cinders are 15 within 1.
    /// </summary>
    [Theory]
    [InlineData(0.0, 15.0)]
    [InlineData(0.1, 15.0)]
    [InlineData(0.25, 15.0)]
    [InlineData(0.5, 15.0)]
    public void A_Full_Searing_Blaze_Generates_About_15_Cinders_Whatever_The_Haste(double haste, double cinders)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<SearingBlaze>();
        f.Wait(40);

        Assert.Equal(cinders, Cinders(f), 1.0);
    }

    /// <summary>
    /// Player: Wildfire makes the ticks come 30% faster but each still gives its full Cinder (divided only by 1 + haste), so a Searing
    /// Blaze with Wildfire on it generates more than 15 Cinders, at any haste.
    /// Dev: Searing Blaze, then Wildfire (it ignores the global cooldown, so it is up for 9 of the burn's 30 seconds); 40 seconds.
    /// The burn gets 9 x 0.3 / 2 = 1.35 more ticks than without, each 1 Cinder divided by the haste, the haste's own ticks adding the
    /// same ticks times the haste: 16.35 at every haste, within 1, and strictly more than 15.
    /// </summary>
    [Theory]
    [InlineData(0.0, 16.35)]
    [InlineData(0.1, 16.35)]
    [InlineData(0.25, 16.35)]
    [InlineData(0.5, 16.35)]
    public void A_Searing_Blaze_With_Wildfire_Generates_More_Than_15_Cinders(double haste, double cinders)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<SearingBlaze>();
        f.Cast<Wildfire>();
        f.Wait(40);

        Assert.True(Cinders(f) > 15.0 + Tolerance, $"expected more than 15 Cinders, got {Cinders(f):F2}");
        Assert.Equal(cinders, Cinders(f), 1.0);
    }

    /// <summary>
    /// Player: haste makes Engulfing Flames tick more often but each tick gives less, so a full burn is still about 30 Cinders.
    /// Dev: one Engulfing Flames and 20 seconds at each haste; the Cinders are 30 within 1.
    /// </summary>
    [Theory]
    [InlineData(0.0, 30.0)]
    [InlineData(0.1, 30.0)]
    [InlineData(0.25, 30.0)]
    [InlineData(0.5, 30.0)]
    public void A_Full_Engulfing_Flames_Generates_About_30_Cinders_Whatever_The_Haste(double haste, double cinders)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<EngulfingFlames>();
        f.Wait(20);

        Assert.Equal(cinders, Cinders(f), 1.0);
    }

    /// <summary>
    /// Player: Wildfire makes the ticks come 30% faster but each still gives its full 5 Cinders (divided only by 1 + haste), so an
    /// Engulfing Flames with Wildfire on it generates more than 30 Cinders, at any haste.
    /// Dev: Engulfing Flames (a 1.5s cast, hasted), then Wildfire as soon as the cast ends: it covers the burn's 9 seconds, so the burn
    /// ticks 6 x 1.3 = 7.8 times at the base rate, 5 Cinders each divided by the haste, the haste's own ticks adding the same ticks times
    /// the haste: 39 at every haste, within 1, and strictly more than 30.
    /// </summary>
    [Theory]
    [InlineData(0.0, 39.0)]
    [InlineData(0.1, 39.0)]
    [InlineData(0.25, 39.0)]
    [InlineData(0.5, 39.0)]
    public void An_Engulfing_Flames_With_Wildfire_Generates_More_Than_30_Cinders(double haste, double cinders)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<EngulfingFlames>();
        f.Cast<Wildfire>();
        f.Wait(20);

        Assert.True(Cinders(f) > 30.0 + Tolerance, $"expected more than 30 Cinders, got {Cinders(f):F2}");
        Assert.Equal(cinders, Cinders(f), 1.0);
    }

    /// <summary>
    /// Player: Cinders cannot be counted in game, Burning Embers can: three Engulfing Flames that tick out in full do not make an Ember
    /// (90 Cinders), four do (120).
    /// Dev: three, then four, Engulfing Flames one after the other on the one enemy, each cast as soon as a charge is back (2 charges of 20
    /// seconds, so the third comes at 20s and the fourth at 40s), then 20 seconds for the last to tick out; 0 Embers with 3, 1 with 4.
    /// </summary>
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void Engulfing_Flames_That_Tick_Out_Make_An_Ember_From_The_Fourth(int casts, int embers)
    {
        var f = StepFights.Ardeos();

        for (int i = 0; i < casts; i++)
        {
            while (!f.TryCast<EngulfingFlames>().Succeeded) f.Wait(0.1);
        }
        f.Wait(20);

        Assert.Equal(embers, f.ArdeosOf().BurningEmbers);
    }

    /// <summary>
    /// Player: starting from 1 Ember and 5 Cinders (seven full Searing Blazes), six more Searing Blazes from Apocalypse make 1 Ember
    /// and 95 Cinders, and with Wildfire 2 Embers and 3 Cinders: Wildfire's extra Cinders push the count over an Ember.
    /// Dev: Searing Blaze cast seven times on the first enemy, each left to tick out in full (31 seconds apart, no Wildfire): 105 Cinders.
    /// Then Apocalypse puts a Searing Blaze on each of the 6 enemies, then (or not) Wildfire, up for 3 to 12 seconds of the burns' 30;
    /// 40 seconds. Each burn gets 9 x 0.3 / 2 = 1.35 more ticks of 1 Cinder: 6 x 16.35 = 98.1 with Wildfire, 90 without; so 203.1 and 195
    /// Cinders, within 1.
    /// </summary>
    [Theory]
    [InlineData(false, 195.0, 1)]
    [InlineData(true, 203.1, 2)]
    public void Wildfire_Adds_About_8_Cinders_To_Six_Searing_Blazes_Enough_For_An_Ember(bool wildfire, double cinders, int embers)
    {
        var f = StepFights.Ardeos(enemies: 6);
        for (int i = 0; i < 7; i++)
        {
            f.Cast<SearingBlaze>();
            f.Wait(31);
        }
        Assert.Equal(105.0, Cinders(f), 1.0);
        Assert.Equal(1, f.ArdeosOf().BurningEmbers);

        f.Cast<Apocalypse>();
        if (wildfire) f.Cast<Wildfire>();
        f.Wait(40);

        Assert.Equal(cinders, Cinders(f), 1.0);
        Assert.Equal(embers, f.ArdeosOf().BurningEmbers);
    }

    /// <summary>
    /// Player: Wildfire makes Engulfing Flames generate more Cinders: Pyromania's three burns (3 enemies) give 90 Cinders, no Ember, and
    /// 117 with Wildfire, an Ember.
    /// Dev: Pyromania on 3 enemies, then (or not) Wildfire right away (both ignore the global cooldown, so it covers the burns' 9 seconds);
    /// 20 seconds. Each burn ticks 6 x 1.3 = 7.8 times for 5 Cinders: 3 x 39 = 117 with Wildfire, 90 without, within 1.
    /// (An Engulfing Flames cast first would add a fourth burn: 120 Cinders, an Ember with or without Wildfire.)
    /// </summary>
    [Theory]
    [InlineData(false, 90.0, 0)]
    [InlineData(true, 117.0, 1)]
    public void Wildfire_Turns_Pyromanias_Three_Engulfing_Flames_Into_An_Ember(bool wildfire, double cinders, int embers)
    {
        var f = StepFights.Ardeos(enemies: 3);

        f.Cast<Pyromania>();
        if (wildfire) f.Cast<Wildfire>();
        f.Wait(20);

        Assert.Equal(cinders, Cinders(f), 1.0);
        Assert.Equal(embers, f.ArdeosOf().BurningEmbers);
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
