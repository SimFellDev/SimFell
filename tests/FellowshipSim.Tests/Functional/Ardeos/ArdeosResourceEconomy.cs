using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's Cinders and Burning Embers: his damage over time effects generate Cinders as they tick, every 100 are a Burning Ember, and
/// Detonate spends an Ember.
///
/// Cinders cannot be counted in game, Burning Embers can: the Ember tests are the ones a player can check. A tick's Cinders are divided by
/// 1 + haste, and the last partial tick's share is read on the server frame it lands on, so a hasted or Wildfire count is within
/// <see cref="CinderTolerance"/> of its formula.
/// </summary>
public class ArdeosResourceEconomy
{
    /// <summary>Tolerance of an exact Cinder count.</summary>
    private const double ExactCinderTolerance = 0.01;

    /// <summary>
    /// Tolerance of a hasted or Wildfire Cinder count. The ticks land on server frames, which moves a count by up to 0.21 Cinders (3
    /// Engulfing Flames with Wildfire: 117.207 for 117). A missed or doubled partial tick moves it by 0.45 Cinders or more.
    /// </summary>
    private const double CinderTolerance = 0.25;

    // ---- Searing Blaze

    /// <summary>
    /// Haste makes Searing Blaze tick more often but each tick gives less, so a full Searing Blaze is still about 15 Cinders.
    /// Haste makes Searing Blaze tick more often but each tick gives less.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    public void A_Full_Searing_Blaze_Generates_About_15_Cinders_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<SearingBlaze>();
        f.Wait(40); // it ticks out

        Assert.Equal(15.0, f.Amount(Ardeos.Cinders), CinderTolerance);
    }

    // ---- Engulfing Flames

    /// <summary>
    /// Haste makes Engulfing Flames tick more often but each tick gives less.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    public void A_Full_Engulfing_Flames_Generates_About_30_Cinders_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<EngulfingFlames>();
        f.Wait(20); // it ticks out

        Assert.Equal(30.0, f.Amount(Ardeos.Cinders), CinderTolerance);
    }

    // ---- Wildfire

    /// <summary>
    /// Wildfire makes the ticks come 30% faster but each still gives its full Cinder (divided only by 1 + haste), so a Searing Blaze with
    /// Wildfire on it generates more than 15 Cinders, at any haste: about 16.35.
    ///
    /// Wildfire is up for 9 of the effect's 30 seconds, so it gets 9 x 0.3 / 2 = 1.35 more ticks than without, each 1 Cinder divided by
    /// 1 + haste, the haste's own ticks adding the same ticks times the haste: 16.35 at every haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    public void A_Searing_Blaze_With_Wildfire_Generates_More_Than_15_Cinders(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<SearingBlaze>();
        f.Cast<Wildfire>(); // off the global cooldown: up for 9 of the effect's 30 seconds
        f.Wait(40); // it ticks out

        using var check = new Checks(f);
        check.True(f.Amount(Ardeos.Cinders) > 15.0 + ExactCinderTolerance);
        check.Equal(16.35, f.Amount(Ardeos.Cinders), CinderTolerance);
    }

    /// <summary>
    /// Wildfire makes the ticks come 30% faster but each still gives its full 5 Cinders (divided only by 1 + haste), so an Engulfing Flames
    /// with Wildfire on it generates more than 30 Cinders, at any haste: about 39.
    ///
    /// Wildfire covers the effect's 9 seconds, so it ticks 6 x 1.3 = 7.8 times at the base rate, 5 Cinders each divided by 1 + haste,
    /// the haste's own ticks adding the same ticks times the haste: 39 at every haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    public void An_Engulfing_Flames_With_Wildfire_Generates_More_Than_30_Cinders(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<EngulfingFlames>();
        f.Cast<Wildfire>(); // as soon as the cast ends: it covers the effect's 9 seconds
        f.Wait(20); // it ticks out

        using var check = new Checks(f);
        check.True(f.Amount(Ardeos.Cinders) > 30.0 + ExactCinderTolerance);
        check.Equal(39.0, f.Amount(Ardeos.Cinders), CinderTolerance);
    }

    /// <summary>
    /// Since Cinders can't be counted in the stronghold, it is tricky to see that Wildfire increases cinder generation.
    /// The following is a scenario a player could test in stronghold.
    /// 
    /// We compare two scenarios:
    /// - casting 6 SB without wildfire: 90 cinders;
    /// - casting 6 SB with wildire: about 98 cinders;
    /// If we start from a situation with 105 cinders, these 2 scenarios differ by 1 ember (visible in stronghold).
    /// This proves that Wildfire does change resource generation.
    /// </summary>
    [Theory]
    [InlineData(false, 195.0, 1)]
    [InlineData(true, 203.1, 2)]
    public void Wildfire_Adds_About_8_Cinders_To_Six_Searing_Blazes_Enough_For_An_Ember(bool wildfire, double cinders, int embers)
    {
        var f = StepFights.Ardeos(enemies: 6);

        for (int i = 0; i < 7; i++)
        {
            f.Cast<SearingBlaze>(); // on the first enemy
            f.Wait(31); // it ticks out
        }

        using var check = new Checks(f);
        check.Equal(105.0, f.Amount(Ardeos.Cinders), ExactCinderTolerance);
        check.Equal(1, f.ArdeosOf().BurningEmbers);

        f.Cast<Apocalypse>(); // a Searing Blaze on each enemy
        if (wildfire) f.Cast<Wildfire>();
        f.Wait(40); // they tick out

        check.Equal(cinders, f.Amount(Ardeos.Cinders), CinderTolerance);
        check.Equal(embers, f.ArdeosOf().BurningEmbers);
    }

    /// <summary>
    /// Since Cinders can't be counted in the stronghold, it is tricky to see that Wildfire increases cinder generation.
    /// The following is a scenario a player could test in stronghold.
    /// 
    /// We compare two scenarios:
    /// - casting 3 EF without wildfire: 90 cinders;
    /// - casting 3 EF with wildire: about 117 cinders;
    /// These 2 scenarios differ by 1 ember (visible in stronghold).
    /// This proves that Wildfire does change resource generation.
    /// </summary>
    [Theory]
    [InlineData(false, 90.0, 0)]
    [InlineData(true, 117.0, 1)]
    public void Wildfire_Turns_Pyromanias_Three_Engulfing_Flames_Into_An_Ember(bool wildfire, double cinders, int embers)
    {
        var f = StepFights.Ardeos(enemies: 3);

        f.Cast<Pyromania>(); // an Engulfing Flames on each enemy
        if (wildfire) f.Cast<Wildfire>(); // both off the global cooldown: it covers the effects' 9 seconds
        f.Wait(20); // they tick out

        using var check = new Checks(f);
        check.Equal(cinders, f.Amount(Ardeos.Cinders), CinderTolerance);
        check.Equal(embers, f.ArdeosOf().BurningEmbers);
    }

    // ---- Burning Embers

    /// <summary>Every 100 Cinders are a Burning Ember, and what is over stays as Cinders.</summary>
    [Fact]
    public void Every_100_Cinders_Are_A_Burning_Ember()
    {
        var f = StepFights.Ardeos();

        using var check = new Checks(f);
        f.Set(Ardeos.Cinders, 99);
        check.Equal(0, f.ArdeosOf().BurningEmbers);
        f.Set(Ardeos.Cinders, 250);
        check.Equal(2, f.ArdeosOf().BurningEmbers);
    }

    /// <summary>
    /// Six Searing Blazes that tick out in full do not make a Burning Ember (90 Cinders), seven do (105).
    /// 
    /// This is testable in stronghold.
    /// </summary>
    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    public void Searing_Blazes_That_Tick_Out_Make_An_Ember_From_The_Seventh(int blazes, int embers)
    {
        var f = StepFights.Ardeos(enemies: blazes);

        for (int i = 0; i < blazes; i++) f.Cast<SearingBlaze>(target: f.Enemies()[i]); // one per enemy: a recast refreshes the effect
        f.Wait(40); // they tick out

        Assert.Equal(embers, f.ArdeosOf().BurningEmbers);
    }

    /// <summary>
    /// Three Engulfing Flames that tick out in full do not make a Burning Ember (90 Cinders), four do (120).
    /// 
    /// This is testable in stronghold.
    /// </summary>
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void Engulfing_Flames_That_Tick_Out_Make_An_Ember_From_The_Fourth(int casts, int embers)
    {
        var f = StepFights.Ardeos();

        for (int i = 0; i < casts; i++)
        {
            f.ResetCooldown<EngulfingFlames>(); // a charge for each
            f.Cast<EngulfingFlames>();
        }
        f.Wait(20); // the last ticks out

        Assert.Equal(embers, f.ArdeosOf().BurningEmbers);
    }

    // ---- Detonate

    /// <summary>Detonate costs a Burning Ember: you cannot cast it without one, and it uses one up (and no other Cinders).</summary>
    [Fact]
    public void Detonate_Costs_A_Burning_Ember()
    {
        var f = StepFights.Ardeos();

        f.Cast<SearingBlaze>(); // Detonate needs an effect to detonate

        using var check = new Checks(f);
        f.Set(Ardeos.Cinders, 99);
        check.False(f.TryCast<Detonate>().Succeeded);

        f.Set(Ardeos.Cinders, 250); // 2 Embers and 50 Cinders
        f.Cast<Detonate>();
        check.Equal(100, f.Spends("Cinders").Single().Value, ExactCinderTolerance);
        check.Equal(1, f.ArdeosOf().BurningEmbers);
    }
}
