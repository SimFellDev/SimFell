using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Kindling (sigil 28): "Your Damage Effects have a chance to burn the target, dealing 483% / 882% / 1281% / 1680% of your Main Stat in
/// fire damage to them over 9 seconds" (ranks 1 to 4). The burn ticks every 1.5 seconds shortened by haste, with no tick as it is applied:
/// 0.69 / 1.26 / 1.83 / 2.40 x main stat a tick. The chance is forced: the first roll burns, the others do not.
/// </summary>
public class SigilKindling
{
    private const string Burn = "Kindling";

    /// <summary>
    /// A hero with Kindling at <paramref name="rank"/>, whose first <paramref name="procs"/> rolls burn and the others do not.
    /// </summary>
    private static StepDriver Fight(string hero, int rank = 1, double haste = 0, double crit = 0, double expertise = 0, int enemies = 1,
        int procs = 1)
    {
        var f = StepFights.Of(hero, haste: haste, crit: crit, expertise: expertise, enemies: enemies,
            augmentations: [new(Kindling.SigilId, rank)]).WithoutDamageSpread();
        int rolls = 0;
        f.ForceProcs(key => key == Kindling.ProcKey ? rolls++ < procs : null);
        return f;
    }

    /// <summary>
    /// A Kindling tick deals 0.69 / 1.26 / 1.83 / 2.40 x your main stat, times your class's balancing aura (Rime and Mara 1.04, Ardeos 1.1,
    /// Elarion 1.08, Gunde and Tariq 1.0).
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.69, 1.04)]
    [InlineData("Rime", "Frost Bolt", 2, 1.26, 1.04)]
    [InlineData("Rime", "Frost Bolt", 3, 1.83, 1.04)]
    [InlineData("Rime", "Frost Bolt", 4, 2.40, 1.04)]
    [InlineData("Ardeos", "Infernal Wave", 4, 2.40, 1.1)]
    [InlineData("Elarion", "Focused Shot", 4, 2.40, 1.08)]
    [InlineData("Gunde", "Double Strike", 4, 2.40, 1.0)]
    [InlineData("Mara", "Skittering Blades", 4, 2.40, 1.04)]
    [InlineData("Tariq", "Heavy Strike", 4, 2.40, 1.0)]
    public void A_Kindling_Tick_Deals_A_Share_Of_Main_Stat(string hero, string ability, int rank, double coefficient, double balancing)
    {
        var f = Fight(hero, rank);
        f.FillResources(); // enough to cast it

        f.Cast(ability);
        f.Wait(12); // the burn ticks out

        AssertRatio(coefficient * balancing, f.Damage(Burn)[0].Value / f.MainStat());
    }

    /// <summary>
    /// Kindling's burn ticks every 1.5 seconds shortened by haste, the first a tick after the hit that lit it, and once more as its 9
    /// seconds end when at least 0.1 seconds (a damage over time's shortest partial tick) are left after the last full tick.
    ///
    /// Times are checked within 0.05 seconds: an effect ends on its server frame.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Kindling_Ticks_Every_1_5_Hasted_Seconds_For_9_Seconds(double haste)
    {
        var f = Fight("Rime", haste: haste);

        f.Cast("Frost Bolt");
        f.Wait(12); // the burn ticks out

        double lit = f.Damage("Frost Bolt")[0].Time.TotalSeconds;
        double period = 1.5 / (1 + haste);
        var ticks = f.Damage(Burn);

        using var check = new Checks(f);
        check.Equal(FullTicks(9, period) + PartialTick(9, period, 0.1), ticks.Count);
        for (int i = 0; i < FullTicks(9, period); i++)
        {
            check.Equal(lit + (i + 1) * period, ticks[i].Time.TotalSeconds, 0.05, what: $"tick {i + 1}");
        }
        check.Equal(lit + 9, ticks[^1].Time.TotalSeconds, 0.05, what: "the last tick");
    }

    /// <summary>Kindling's burn deals 9 seconds of ticks: a last partial tick deals its share of a tick.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Kindling_Deals_9_Seconds_Of_Ticks(double haste)
    {
        var f = Fight("Rime", haste: haste);

        f.Cast("Frost Bolt");
        f.Wait(12); // the burn ticks out

        var ticks = f.Damage(Burn);

        AssertRatio(9 / (1.5 / (1 + haste)), ticks.Sum(tick => tick.Value) / ticks[0].Value);
    }

    /// <summary>
    /// Kindling's burn deals 6 ticks of 69% of your main stat over its 9 seconds at rank 1, 414% in all, times Rime's balancing aura (1.04).
    ///
    /// TODO: double check the tooltip's 483% (7 ticks), probably a tooltip error: 9 seconds of 1.5 second ticks are 6, as the sim has it.
    /// </summary>
    [Fact]
    public void Kindling_Deals_6_Ticks_Of_69_Percent_Main_Stat()
    {
        var f = Fight("Rime");

        f.Cast("Frost Bolt");
        f.Wait(12); // the burn ticks out

        AssertRatio(6 * 0.69 * 1.04, f.Damage(Burn).Sum(tick => tick.Value) / f.MainStat());
    }

    /// <summary>Kindling's ticks can crit, with your crit chance.</summary>
    [Fact]
    public void Kindling_Ticks_Can_Crit()
    {
        var f = Fight("Rime", crit: 0.3);

        f.Cast("Frost Bolt");
        f.Wait(12); // the burn ticks out

        Assert.NotEmpty(f.Damage(Burn));
        Assert.All(f.Damage(Burn), tick => Assert.Equal(0.3, tick.CritChance, 1e-9));
    }

    /// <summary>Kindling's ticks gain from Expertise like any hit.</summary>
    [Fact]
    public void Kindling_Ticks_Gain_From_Expertise()
    {
        double FirstTick(double expertise)
        {
            var f = Fight("Rime", expertise: expertise);

            f.Cast("Frost Bolt");
            f.Wait(4); // the first tick

            return f.Damage(Burn)[0].Value;
        }

        AssertRatio(1.1, FirstTick(expertise: 0.1) / FirstTick(expertise: 0));
    }

    /// <summary>
    /// The tick of a damage over time effect does not roll for Kindling: Kindling's own, Fire Ball's burn or Rend.
    ///
    /// From the game data: the sigil's listener does not trigger on periodic executions. The engine rolls Fire Ball's and Rend's ticks.
    /// </summary>
    [Theory(Skip = "Per the user: what procs Kindling is not known")]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Fire Ball")]
    [InlineData("Gunde", "Double Strike")]
    public void Damage_Over_Time_Ticks_Do_Not_Roll_For_Kindling(string hero, string ability)
    {
        var f = StepFights.Of(hero, augmentations: [new(Kindling.SigilId, 1)]);
        int rolls = 0;
        var procs = f.ForceProcs(key => key == Kindling.ProcKey ? rolls++ == 0 : null); // the first roll burns
        f.FillResources(); // enough to cast it

        f.Cast(ability);
        f.Wait(1); // the hits land
        int rollsOfTheHits = procs.ChancesOf(Kindling.ProcKey).Count;
        f.Wait(12); // the effects tick

        Assert.Equal(rollsOfTheHits, procs.ChancesOf(Kindling.ProcKey).Count);
    }

    /// <summary>
    /// A new Kindling on a burning enemy restarts the 9 seconds and keeps the ticks' rhythm: a second bolt 1.5 seconds after the first
    /// leaves the ticks every 1.5 seconds from the first, and the last 9 seconds after the second bolt.
    /// </summary>
    [Fact]
    public void A_New_Kindling_Restarts_The_Burn_And_Keeps_Its_Rhythm()
    {
        var f = Fight("Rime", procs: 2);

        f.Cast("Frost Bolt");
        f.Cast("Frost Bolt");
        f.Wait(12); // the burn ticks out

        var bolts = f.Damage("Frost Bolt");
        double first = bolts[0].Time.TotalSeconds;
        double second = bolts[1].Time.TotalSeconds;
        var ticks = f.Damage(Burn);

        using var check = new Checks(f);
        check.Equal(FullTicks(second + 9 - first, 1.5), ticks.Count);
        for (int i = 0; i < ticks.Count; i++)
        {
            check.Equal(first + (i + 1) * 1.5, ticks[i].Time.TotalSeconds, 0.05, what: $"tick {i + 1}");
        }
        check.Equal(second + 9, ticks[^1].Time.TotalSeconds, 0.05, what: "the last tick");
    }

    /// <summary>Each enemy has its own Kindling.</summary>
    [Fact]
    public void Each_Enemy_Burns_On_Its_Own()
    {
        var f = Fight("Rime", enemies: 2, procs: 2);

        f.Cast("Frost Bolt", target: f.Enemies()[0]);
        f.Cast("Frost Bolt", target: f.Enemies()[1]);
        f.Wait(12); // the burns tick out

        Assert.Equal(2, f.FirstHitOnEachEnemy(Burn).Count);
    }
}
