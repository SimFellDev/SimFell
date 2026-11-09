using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's Energy (200 at most, spent by her abilities, regenerating) and Combo Points (6 at most, built by her generators and spent by
/// her finishers), and what a Spirit Refund gives her. What a Spirit Refund gives every hero is in Generic/SpiritRefund.cs.
///
/// Energy regenerates all the time, so the amounts spent and gained are read off the fight's events, not off the pool.
/// </summary>
public class MaraResourceEconomy
{
    private const double Tolerance = 0.01;

    /// <summary>Mara starts a fight with full Energy, 200, and no Combo Points.</summary>
    [Fact]
    public void Mara_Starts_With_200_Energy_And_No_Combo_Points()
    {
        var f = StepFights.Mara();

        using var check = new Checks(f);
        check.Equal(200, f.Amount(Mara.Energy), Tolerance);
        check.Equal(0, f.Amount(Mara.ComboPoints), Tolerance);
    }

    // ---- Generators

    /// <summary>
    /// Each generator costs Energy and builds Combo Points: Backstab 20 Energy and 2 Combo Points, Skittering Blades 35 Energy and 1
    /// Combo Point for each enemy hit (one here), Widow's Bite no Energy and 4 Combo Points (2 for each of its two strikes).
    /// </summary>
    [Theory]
    [InlineData("Backstab", 20, 2)]
    [InlineData("Skittering Blades", 35, 1)]
    [InlineData("Widow's Bite", 0, 4)]
    public void Generators_Cost_Energy_And_Build_Combo_Points(string ability, double energy, double points)
    {
        var f = StepFights.Mara();

        f.Cast(ability);

        using var check = new Checks(f);
        check.Equal(energy, f.Spends("Energy").Sum(spend => spend.Value), Tolerance);
        check.Equal(points, f.Amount(Mara.ComboPoints), Tolerance);
    }

    /// <summary>Skittering Blades builds a Combo Point for each enemy it hits: 3 with 3 enemies.</summary>
    [Fact]
    public void Skittering_Blades_Builds_A_Combo_Point_Per_Enemy_Hit()
    {
        var f = StepFights.Mara(enemies: 3);

        f.Cast<SkitteringBlades>();

        Assert.Equal(3, f.Amount(Mara.ComboPoints), Tolerance);
    }

    /// <summary>
    /// A critical strike builds one more Combo Point from each hit: Backstab 3, Skittering Blades 2 for each enemy hit, Widow's Bite 3
    /// for each of its two strikes (6).
    ///
    /// The hero has 100% crit chance, so every hit crits.
    /// </summary>
    [Theory]
    [InlineData("Backstab", 3)]
    [InlineData("Skittering Blades", 2)]
    [InlineData("Widow's Bite", 6)]
    public void A_Crit_Builds_One_More_Combo_Point_Per_Hit(string ability, double points)
    {
        var f = StepFights.Mara(crit: 1.0);

        f.Cast(ability);

        Assert.Equal(points, f.Amount(Mara.ComboPoints), Tolerance);
    }

    /// <summary>Widow's Bite generates 30 Energy.</summary>
    [Fact]
    public void Widows_Bite_Generates_30_Energy()
    {
        var f = StepFights.Mara();
        f.Set(Mara.Energy, 100); // so the gain is not lost at the cap

        f.Cast<WidowsBite>();

        Assert.Equal(30, f.Gains("Energy").Sum(gain => gain.Value), Tolerance);
    }

    /// <summary>You hold 6 Combo Points at most: three Widow's Bites (4 each) leave 6.</summary>
    [Fact]
    public void Combo_Points_Stop_At_6()
    {
        var f = StepFights.Mara();

        f.Cast<WidowsBite>();
        f.Cast<WidowsBite>();
        f.Cast<WidowsBite>();

        Assert.Equal(6, f.Amount(Mara.ComboPoints), Tolerance);
    }

    /// <summary>You cannot cast an ability without the Energy for it: Backstab (20 Energy) is refused at 19 Energy and allowed at 20.</summary>
    [Fact]
    public void An_Ability_Needs_Its_Energy()
    {
        var f = StepFights.Mara();

        using var check = new Checks(f);
        f.Set(Mara.Energy, 19);
        check.False(f.TryCast<Backstab>().Succeeded);
        f.Set(Mara.Energy, 20);
        check.True(f.TryCast<Backstab>().Succeeded);
    }

    // ---- Finishers

    /// <summary>
    /// A finisher spends every Combo Point you hold, and costs Energy: Queen's Fang 40, Arachnid Assault 45, Hemorrhaging Strike 20. With
    /// 4 Combo Points, it spends its Energy and the 4 Combo Points, with none left.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", 40)]
    [InlineData("Arachnid Assault", 45)]
    [InlineData("Hemorrhaging Strike", 20)]
    public void Finishers_Spend_Every_Combo_Point_And_Their_Energy(string ability, double energy)
    {
        var f = StepFights.Mara();
        f.Set(Mara.ComboPoints, 4);

        f.Cast(ability);

        using var check = new Checks(f);
        check.Equal(energy, f.Spends("Energy").Sum(spend => spend.Value), Tolerance);
        check.Equal(4, f.Spends("Combo Points").Sum(spend => spend.Value), Tolerance);
        check.Equal(0, f.Amount(Mara.ComboPoints), Tolerance);
    }

    /// <summary>A finisher needs at least one Combo Point: with none it is refused, with one (in a fight of its own) it is allowed.</summary>
    [Theory]
    [InlineData("Queen's Fang")]
    [InlineData("Arachnid Assault")]
    [InlineData("Hemorrhaging Strike")]
    public void A_Finisher_Needs_A_Combo_Point(string ability)
    {
        var noPointScenario = StepFights.Mara();
        noPointScenario.Set(Mara.ComboPoints, 0);

        var onePointScenario = StepFights.Mara();
        onePointScenario.Set(Mara.ComboPoints, 1);

        Assert.False(noPointScenario.TryCast(ability).Succeeded);
        Assert.True(onePointScenario.TryCast(ability).Succeeded);
    }

    // ---- Energy regeneration

    /// <summary>The Energy regenerated from 0 in <paramref name="seconds"/>, after <paramref name="prepare"/>.</summary>
    private static double RegeneratedIn(double haste, double seconds, Action<StepDriver>? prepare = null)
    {
        var f = StepFights.Mara(haste);
        f.Set(Mara.Energy, 0);
        prepare?.Invoke(f);
        WaitReading(f, seconds);
        return f.MaraOf().EnergyAt(f.Now);
    }

    /// <summary>Lets time pass in 10ms steps, so rates that change are read again.</summary>
    private static void WaitReading(StepDriver f, double seconds)
    {
        var end = f.Now + SimTime.FromSeconds(seconds);
        while (f.Now < end) f.Wait(0.01);
    }

    /// <summary>
    /// Energy regenerates at about 15 per second: from 0, between 72.5 and 77.5 after 5 seconds (15 per second within half a point).
    ///
    /// TODO: the rate is fitted to combat logs (15, within 0.5 per second; the game data has no value). Check in game.
    /// </summary>
    [Fact]
    public void Energy_Regenerates_About_15_Per_Second()
    {
        Assert.InRange(RegeneratedIn(0, 5), 72.5, 77.5);
    }

    /// <summary>
    /// Haste speeds up Energy regeneration: 15 x (1 + haste) per second. What 5 seconds regenerate from 0 is 1 + haste times what they
    /// regenerate at 0% haste.
    ///
    /// Checked within 1%.
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Haste_Speeds_Up_Energy_Regeneration(double haste)
    {
        AssertRatio(1 + haste, RegeneratedIn(haste, 5) / RegeneratedIn(0, 5), 0.01);
    }

    /// <summary>Energy stops at 200: at 190 Energy, 10 seconds later you hold 200.</summary>
    [Fact]
    public void Energy_Stops_At_200()
    {
        var f = StepFights.Mara();
        f.Set(Mara.Energy, 190);

        WaitReading(f, 10);

        Assert.Equal(200, f.MaraOf().EnergyAt(f.Now), Tolerance);
    }

    /// <summary>
    /// Maiden of Death raises your Energy regeneration by 20% while it lasts: 5 seconds of regeneration from 0 with it up give 1.2 times
    /// what they give without it.
    ///
    /// Checked within 1%.
    /// </summary>
    [Fact]
    public void Maiden_Of_Death_Raises_Energy_Regeneration_By_20_Percent()
    {
        double maidenEnergy = RegeneratedIn(0, 5, f => f.Cast<MaidenOfDeath>());
        double plainEnergy = RegeneratedIn(0, 5);

        AssertRatio(1.2, maidenEnergy / plainEnergy, 0.01);
    }

    /// <summary>
    /// While Seething Poison is on an enemy you gain Predator's Rush, 20% more Energy regeneration. A Widow's Bite from Stealth puts
    /// Seething Poison on the enemy; then 5 seconds of regeneration from 0 give 1.2 times what they give without it.
    ///
    /// Energy is set to 0 after the Widow's Bite, so its own gain is not counted. Checked within 2%.
    /// </summary>
    [Fact]
    public void Predators_Rush_Raises_Energy_Regeneration_By_20_Percent()
    {
        var rushScenario = StepFights.Mara();

        rushScenario.Cast<BroodingShadows>(); // Stealth
        rushScenario.Cast<WidowsBite>();
        rushScenario.Set(Mara.Energy, 0); // the Widow's Bite's own gain
        WaitReading(rushScenario, 5);
        double rushEnergy = rushScenario.MaraOf().EnergyAt(rushScenario.Now);

        AssertRatio(1.2, rushEnergy / RegeneratedIn(0, 5), 0.02);
    }

    /// <summary>
    /// Hemorrhaging Strike's bleed gives 2 Energy with each tick. After a Hemorrhaging Strike with 6 Combo Points (a 30 second bleed), 12.5
    /// seconds bring 4 ticks and 4 gains of 2 Energy.
    ///
    /// Energy is set to 0 after the strike and 12.5 seconds are not enough to reach the cap, where a gain is lost.
    /// </summary>
    [Fact]
    public void Each_Hemorrhage_Tick_Gives_2_Energy()
    {
        var f = StepFights.Mara();
        f.Set(Mara.ComboPoints, 6);

        f.Cast<HemorrhagingStrike>();
        f.Set(Mara.Energy, 0); // so no gain is lost at the cap
        f.Wait(12.5);
        int ticks = f.Damage("Hemorrhage").Count;

        using var check = new Checks(f);
        check.Equal(4, ticks);
        check.Equal(ticks, f.Gains("Energy").Count(gain => Math.Abs(gain.Value - 2) < Tolerance));
    }

    // ---- Efficient Killer

    /// <summary>With Efficient Killer, each Combo Point you spend generates 1 Energy: a Queen's Fang with 5 Combo Points gives a gain of 5
    /// Energy.</summary>
    [Fact]
    public void Efficient_Killer_Generates_1_Energy_Per_Combo_Point_Spent()
    {
        var f = StepFights.Mara(talents: [EfficientKiller.TalentId]);
        f.Set(Mara.Energy, 100); // so the gain is not lost at the cap
        f.Set(Mara.ComboPoints, 5);

        f.Cast<QueensFang>();

        Assert.Contains(f.Gains("Energy"), gain => Math.Abs(gain.Value - 5) < Tolerance);
    }

    // ---- Venomous Delight

    /// <summary>
    /// With Venomous Delight, each time you deal poison damage to an enemy you have a 10% chance to generate 10 Energy: after a Widow's
    /// Bite from Stealth, each Seething Poison tick rolls at 10%, and forced on, a tick gives a gain of 10 Energy.
    /// </summary>
    [Fact]
    public void Venomous_Delight_Has_A_10_Percent_Chance_Of_10_Energy_Per_Poison_Hit()
    {
        var f = StepFights.Mara(talents: [VenomousDelight.TalentId]);
        var procs = f.ForceProcs(key => key == VenomousDelight.ProcKey ? true : null);

        f.Cast<BroodingShadows>(); // Stealth
        f.Cast<WidowsBite>();
        f.Set(Mara.Energy, 100); // so the gain is not lost at the cap

        f.Wait(2.5); // the first ticks

        using var check = new Checks(f);
        check.True(f.Damage("Seething Poison").Count > 0, "Seething Poison ticks");
        foreach (double chance in procs.ChancesOf(VenomousDelight.ProcKey))
            check.Equal(0.1, chance, what: "a Venomous Delight roll's chance");
        check.True(f.Gains("Energy").Any(gain => Math.Abs(gain.Value - 10) < Tolerance), "an Energy gain of 10");
    }

    // ---- Spirit Refund

    /// <summary>A Spirit Refund on a finisher gives back the Combo Points it spent and the Energy it cost, and a Spirit Point.</summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_Combo_Points_Energy_And_A_Spirit_Point()
    {
        var f = StepFights.Mara(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Set(Mara.ComboPoints, 6);
        f.Set(Mara.Energy, 100); // so the gain is not lost at the cap

        f.Cast<QueensFang>(); // 40 Energy
        f.Wait(0.5); // the refund lands

        using var check = new Checks(f);
        check.Equal(6, f.Amount(Mara.ComboPoints), Tolerance);
        check.True(f.Gains("Energy").Any(gain => Math.Abs(gain.Value - 40) < Tolerance), "an Energy gain of 40");
        check.Equal(1, f.Amount(Hero.SpiritPoints), Tolerance);
    }

    /// <summary>Spirit Refund only comes from abilities that spend Combo Points: a Backstab and a Widow's Bite roll no refund and give no
    /// Spirit Point.</summary>
    [Fact]
    public void Generators_Do_Not_Roll_A_Spirit_Refund()
    {
        var f = StepFights.Mara(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null); // a roll would succeed

        f.Cast<Backstab>();
        f.Cast<WidowsBite>();

        using var check = new Checks(f);
        check.Equal(0, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls");
        check.Equal(0, f.Amount(Hero.SpiritPoints), Tolerance);
    }
}
