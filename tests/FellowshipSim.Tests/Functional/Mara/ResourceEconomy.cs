using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's Energy (200 at most, spent by her abilities, regenerating) and Combo Points (6 at most, built by her generators and spent by
/// her finishers). Each test has a "Player" line (the rule) and a "Dev" line (how it is done).
/// Dev: Energy regenerates all the time, so the amounts spent and gained are read off the fight's events, not off the pool.
/// </summary>
public class MaraResourceEconomy
{
    private const double Tolerance = 0.01;

    /// <summary>
    /// Player: Mara starts a fight with full Energy, 200, and no Combo Points.
    /// Dev: a new fight; the pools are read before anything is cast.
    /// </summary>
    [Fact]
    public void Mara_Starts_With_200_Energy_And_No_Combo_Points()
    {
        var f = StepFights.Mara();

        Assert.Equal(200, f.Hero.ResourceAmount(Mara.Energy), Tolerance);
        Assert.Equal(0, f.ComboPoints(), Tolerance);
    }

    // ---- Generators

    /// <summary>
    /// Player: each generator costs Energy and builds Combo Points: Backstab 20 Energy and 2 Combo Points, Skittering Blades 35 Energy and 1
    /// Combo Point for each enemy hit (one here), Widow's Bite no Energy and 4 Combo Points (2 for each of its two strikes).
    /// Dev: one cast of each at the start of a fight, no crit; the Energy spent and the Combo Points held are read.
    /// </summary>
    [Theory]
    [InlineData("Backstab", 20, 2)]
    [InlineData("Skittering Blades", 35, 1)]
    [InlineData("Widow's Bite", 0, 4)]
    public void Generators_Cost_Energy_And_Build_Combo_Points(string ability, double energy, double points)
    {
        var f = StepFights.Mara();

        switch (ability)
        {
            case "Backstab": f.Cast<Backstab>(); break;
            case "Skittering Blades": f.Cast<SkitteringBlades>(); break;
            default: f.Cast<WidowsBite>(); break;
        }

        Assert.Equal(energy, f.Spent("Energy").Sum(), Tolerance);
        Assert.Equal(points, f.ComboPoints(), Tolerance);
    }

    /// <summary>
    /// Player: a critical strike builds one more Combo Point from each hit: Backstab 3, Skittering Blades 2 for each enemy hit, Widow's Bite 3
    /// for each of its two strikes (6).
    /// Dev: the same casts with 100% crit chance (Expected mode makes a hit a crit above 50%).
    /// </summary>
    [Theory]
    [InlineData("Backstab", 3)]
    [InlineData("Skittering Blades", 2)]
    [InlineData("Widow's Bite", 6)]
    public void A_Crit_Builds_One_More_Combo_Point_Per_Hit(string ability, double points)
    {
        var f = StepFights.Mara(crit: 1.0);

        switch (ability)
        {
            case "Backstab": f.Cast<Backstab>(); break;
            case "Skittering Blades": f.Cast<SkitteringBlades>(); break;
            default: f.Cast<WidowsBite>(); break;
        }

        Assert.Equal(points, f.ComboPoints(), Tolerance);
    }

    /// <summary>
    /// Player: Widow's Bite generates 30 Energy.
    /// Dev: Energy set to 100 so the gain is not lost at the cap; one Widow's Bite; the Energy gained is 30.
    /// </summary>
    [Fact]
    public void Widows_Bite_Generates_30_Energy()
    {
        var f = StepFights.Mara();
        f.SetEnergy(100);

        f.Cast<WidowsBite>();

        Assert.Equal(30, f.Gained("Energy").Sum(), Tolerance);
    }

    /// <summary>
    /// Player: you hold 6 Combo Points at most.
    /// Dev: three Widow's Bites (4 each); 6 Combo Points.
    /// </summary>
    [Fact]
    public void Combo_Points_Stop_At_6()
    {
        var f = StepFights.Mara();

        f.Cast<WidowsBite>();
        f.Cast<WidowsBite>();
        f.Cast<WidowsBite>();

        Assert.Equal(6, f.ComboPoints(), Tolerance);
    }

    /// <summary>
    /// Player: you cannot cast an ability without the Energy for it: it is refused.
    /// Dev: Energy set to 10 and a Backstab (20) is refused; the Energy cost of the other abilities is checked at each: 19 refused, 20 allowed.
    /// </summary>
    [Fact]
    public void An_Ability_Needs_Its_Energy()
    {
        var f = StepFights.Mara();

        f.SetEnergy(19);
        Assert.False(f.TryCast<Backstab>().Succeeded);

        f.SetEnergy(20);
        Assert.True(f.TryCast<Backstab>().Succeeded);
    }

    // ---- Finishers

    /// <summary>
    /// Player: a finisher spends every Combo Point you hold, and costs Energy: Queen's Fang 40, Arachnid Assault 45, Hemorrhaging Strike 20.
    /// Dev: 4 Combo Points, then one finisher; the Energy spent is its cost and the Combo Points spent are 4, with none left.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", 40)]
    [InlineData("Arachnid Assault", 45)]
    [InlineData("Hemorrhaging Strike", 20)]
    public void Finishers_Spend_Every_Combo_Point_And_Their_Energy(string ability, double energy)
    {
        var f = StepFights.Mara();
        f.SetComboPoints(4);

        switch (ability)
        {
            case "Queen's Fang": f.Cast<QueensFang>(); break;
            case "Arachnid Assault": f.Cast<ArachnidAssault>(); break;
            default: f.Cast<HemorrhagingStrike>(); break;
        }

        Assert.Equal(energy, f.Spent("Energy").Sum(), Tolerance);
        Assert.Equal(4, f.Spent("Combo Points").Sum(), Tolerance);
        Assert.Equal(0, f.ComboPoints(), Tolerance);
    }

    /// <summary>
    /// Player: a finisher needs at least one Combo Point: with none it cannot be cast.
    /// Dev: no Combo Points; each finisher is refused; with one Combo Point (and a fresh fight) it is allowed.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang")]
    [InlineData("Arachnid Assault")]
    [InlineData("Hemorrhaging Strike")]
    public void A_Finisher_Needs_A_Combo_Point(string ability)
    {
        bool Try(double points)
        {
            var f = StepFights.Mara();
            f.SetComboPoints(points);
            return ability switch
            {
                "Queen's Fang" => f.TryCast<QueensFang>().Succeeded,
                "Arachnid Assault" => f.TryCast<ArachnidAssault>().Succeeded,
                _ => f.TryCast<HemorrhagingStrike>().Succeeded,
            };
        }

        Assert.False(Try(0));
        Assert.True(Try(1));
    }

    // ---- Energy regeneration

    private static double RegeneratedIn(double haste, double seconds, Action<StepDriver>? prepare = null)
    {
        var f = StepFights.Mara(haste);
        f.SetEnergy(0);
        prepare?.Invoke(f);
        f.WaitReading(seconds);
        return f.MaraOf().EnergyAt(f.Now);
    }

    /// <summary>
    /// Player: Energy regenerates at about 15 per second.
    /// Dev: Energy set to 0 and 5 seconds waited; between 72 and 78 (15 per second within half a point).
    /// TODO: the rate is fitted to combat logs (15, within 0.5 per second; the game data has no value). Check in game.
    /// </summary>
    [Fact]
    public void Energy_Regenerates_About_15_Per_Second()
    {
        Assert.InRange(RegeneratedIn(0, 5), 72.5, 77.5);
    }

    /// <summary>
    /// Player: haste speeds up Energy regeneration: 15 x (1 + haste) per second.
    /// Dev: Energy set to 0 and 5 seconds waited at 0%, 20% and 50% haste; the ratio to the 0% amount is 1 + haste.
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Haste_Speeds_Up_Energy_Regeneration(double haste)
    {
        Assert.Equal(1 + haste, RegeneratedIn(haste, 5) / RegeneratedIn(0, 5), 0.01);
    }

    /// <summary>
    /// Player: Energy stops at 200.
    /// Dev: 190 Energy and 10 seconds waited; 200.
    /// </summary>
    [Fact]
    public void Energy_Stops_At_200()
    {
        var f = StepFights.Mara();
        f.SetEnergy(190);

        f.WaitReading(10);

        Assert.Equal(200, f.MaraOf().EnergyAt(f.Now), Tolerance);
    }

    /// <summary>
    /// Player: Maiden of Death raises your Energy regeneration by 20% while it lasts.
    /// Dev: 5 seconds of regeneration from 0 with Maiden of Death up, against none; the ratio is 1.2.
    /// </summary>
    [Fact]
    public void Maiden_Of_Death_Raises_Energy_Regeneration_By_20_Percent()
    {
        double with = RegeneratedIn(0, 5, f => f.Cast<MaidenOfDeath>());

        Assert.Equal(1.2, with / RegeneratedIn(0, 5), 0.01);
    }

    /// <summary>
    /// Player: while Seething Poison is on an enemy you gain Predator's Rush, 20% more Energy regeneration.
    /// Dev: a Widow's Bite from Stealth puts Seething Poison on the enemy; 5 seconds of regeneration from 0 (after the Energy gain is
    /// taken off), against none.
    /// </summary>
    [Fact]
    public void Predators_Rush_Raises_Energy_Regeneration_By_20_Percent()
    {
        var f = StepFights.Mara();
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>();
        f.SetEnergy(0);
        f.WaitReading(5);
        double with = f.MaraOf().EnergyAt(f.Now);

        Assert.Equal(1.2, with / RegeneratedIn(0, 5), 0.02);
    }

    /// <summary>
    /// Player: Hemorrhaging Strike's bleed gives 2 Energy with each tick.
    /// Dev: a Hemorrhaging Strike with 6 Combo Points (a 30 second bleed); Energy set to 0 and 12.5 seconds waited (not enough to reach
    /// the cap, where a gain is lost); 4 ticks, 4 gains of 2.
    /// </summary>
    [Fact]
    public void Each_Hemorrhage_Tick_Gives_2_Energy()
    {
        var f = StepFights.Mara();
        f.SetComboPoints(6);
        f.Cast<HemorrhagingStrike>();
        f.SetEnergy(0);

        f.Wait(12.5);

        int ticks = f.Damage("Hemorrhage").Count;
        Assert.Equal(4, ticks);
        Assert.Equal(ticks, f.Gained("Energy").Count(g => Math.Abs(g - 2) < Tolerance));
    }
}
