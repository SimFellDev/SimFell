using FellowshipSim.Core.Customization.Legendaries;
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
    /// Energy regenerates at 15 per second, and haste speeds it up: 15 x (1 + haste) per second. From 0, 5 seconds regenerate 75 x (1 +
    /// haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Energy_Regenerates_15_Per_Second_Times_1_Plus_Haste(double haste)
    {
        AssertRatio(75 * (1 + haste), RegeneratedIn(haste, 5));
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
    /// Maiden of Death and Predator's Rush stack on Energy regeneration: both up, 5 seconds of regeneration from 0 give 1.2 x 1.2 = 1.44
    /// times what they give with neither (they multiply, not add to 1.4: the engine's way, to check in game).
    ///
    /// Energy is set to 0 after the casts, so their own gains are not counted. Checked within 2%.
    /// </summary>
    [Fact]
    public void Maiden_Of_Death_And_Predators_Rush_Multiply()
    {
        var f = StepFights.Mara();

        f.Cast<MaidenOfDeath>();
        f.Cast<BroodingShadows>(); // Stealth
        f.Cast<WidowsBite>(); // Seething Poison: Predator's Rush
        f.Set(Mara.Energy, 0);
        WaitReading(f, 5);
        double bothEnergy = f.MaraOf().EnergyAt(f.Now);

        AssertRatio(1.2 * 1.2, bothEnergy / RegeneratedIn(0, 5), 0.02);
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

    /// <summary>The damage names of Mara's poisons: what Venomous Delight rolls for.</summary>
    private static readonly string[] PoisonHits =
        ["Seething Poison", "Volatile Poison", "Caustic Poison", "Corrosive Spill", "Seething Burst", "Hemotoxin", "Vexira's Venom"];

    /// <summary>A fight with Venomous Delight in which <paramref name="source"/> deals its poison damage, and the Venomous Delight rolls.</summary>
    private static (StepDriver Fight, ForcedProcs Rolls) PoisonFight(string source)
    {
        int[] talents = source switch
        {
            "Corrosive Spill" => [VenomousDelight.TalentId, CorrosiveSpill.TalentId],
            "Seething Burst" => [VenomousDelight.TalentId, SeethingBurst.TalentId],
            "Hemotoxin" or "Hemotoxin burst" => [VenomousDelight.TalentId, Hemotoxin.TalentId],
            _ => [VenomousDelight.TalentId],
        };
        int enemies = source is "Volatile Poison" or "Seething Burst" or "Corrosive Spill" or "Hemotoxin burst" ? 3 : 1;
        var f = StepFights.Mara(enemies: enemies, talents: talents,
            legendaries: source == "Vexira's Venom" ? [VexirasVenom.WristItemId] : null);
        var rolls = f.ForceProcs(key => key switch
        {
            VenomousDelight.ProcKey => false, // only to read its rolls
            CorrosiveSpill.ProcKey or SeethingBurst.ProcKey or Hemotoxin.ProcKey => true,
            _ => null,
        });

        switch (source)
        {
            case "Seething Poison" or "Seething Burst":
                f.Cast<BroodingShadows>(); // into Stealth
                f.Cast<WidowsBite>();
                break;
            case "Caustic Poison":
                f.Cast<BroodingShadows>(); // into Stealth
                f.Cast<StealthBackstab>();
                break;
            case "Volatile Poison":
                f.Cast<BroodingShadows>(); // into Stealth
                f.Cast<StealthSkitteringBlades>();
                break;
            case "Corrosive Spill":
                f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang
                f.Cast<QueensFang>();
                break;
            case "Hemotoxin":
                f.Cast<Backstab>(); // a stack
                break;
            case "Hemotoxin burst":
                f.Cast<Backstab>(); // a stack
                f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike
                f.Cast<HemorrhagingStrike>(); // consumes it
                break;
            case "Vexira's Venom":
                f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang
                f.Cast<QueensFang>(crit: true);
                break;
        }
        f.Wait(10); // the poison ticks and erupts
        return (f, rolls);
    }

    /// <summary>
    /// With Venomous Delight, each time you deal poison damage to an enemy you have a 10% chance to generate 10 Energy: every hit of every
    /// poison rolls once at 10%, ticks, eruptions and area hits too (a Volatile Poison on 3 enemies, Seething Burst and Corrosive Spill
    /// among them), whatever brings them: Seething Poison, Caustic Poison, Volatile Poison and its eruption, Corrosive Spill, Seething
    /// Burst, Hemotoxin's poison and its burst, and Vexira's Venom.
    ///
    /// The roll is forced off, so Energy plays no part: the rolls are counted against the poison hits of the fight.
    /// </summary>
    [Theory]
    [InlineData("Seething Poison")]
    [InlineData("Caustic Poison")]
    [InlineData("Volatile Poison")]
    [InlineData("Corrosive Spill")]
    [InlineData("Seething Burst")]
    [InlineData("Hemotoxin")]
    [InlineData("Hemotoxin burst")]
    [InlineData("Vexira's Venom")]
    public void Venomous_Delight_Rolls_At_10_Percent_On_Every_Poison_Hit(string source)
    {
        var (f, rolls) = PoisonFight(source);
        int poisonHits = f.AllDamage().Count(hit => PoisonHits.Contains(hit.Name) && hit.Value > 0);
        var chances = rolls.ChancesOf(VenomousDelight.ProcKey);

        using var check = new Checks(f);
        check.True(poisonHits > 0, $"{source} deals damage");
        check.Equal(poisonHits, chances.Count, what: "Venomous Delight rolls");
        foreach (double chance in chances) check.Equal(0.1, chance, what: "a Venomous Delight roll's chance");
    }

    /// <summary>Forced on, a Venomous Delight roll gives 10 Energy.</summary>
    [Fact]
    public void A_Venomous_Delight_Proc_Gives_10_Energy()
    {
        var f = StepFights.Mara(talents: [VenomousDelight.TalentId]);
        f.ForceProcs(key => key == VenomousDelight.ProcKey ? true : null);

        f.Cast<BroodingShadows>(); // Stealth
        f.Cast<WidowsBite>();
        f.Set(Mara.Energy, 100); // so the gain is not lost at the cap
        f.Wait(2.5); // the first tick

        Assert.Contains(f.Gains("Energy"), gain => Math.Abs(gain.Value - 10) < Tolerance);
    }

    /// <summary>Bleeds do not roll for Venomous Delight: Hemorrhaging Strike's hit and its bleed's ticks deal damage and roll nothing.</summary>
    [Fact]
    public void Venomous_Delight_Ignores_Bleeds()
    {
        var f = StepFights.Mara(talents: [VenomousDelight.TalentId]);
        var rolls = f.ForceProcs(key => key == VenomousDelight.ProcKey ? false : null);
        f.Set(Mara.ComboPoints, 2); // a 18 second bleed

        f.Cast<HemorrhagingStrike>();
        f.Wait(20); // the bleed ticks out

        using var check = new Checks(f);
        check.True(f.Damage("Hemorrhage").Count > 0, "the bleed ticks");
        check.Equal(0, rolls.ChancesOf(VenomousDelight.ProcKey).Count, what: "Venomous Delight rolls");
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
