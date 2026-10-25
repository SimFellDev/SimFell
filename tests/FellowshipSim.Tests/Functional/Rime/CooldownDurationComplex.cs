using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's cooldown effects working together. One effect at a time is in CooldownDurationSimple.cs.
/// Dev: for effects that shorten a cooldown the ready time is read from the cooldown state (now + time remaining). For effects
/// that make a cooldown tick faster, the fight is advanced 10ms at a time until the ability is ready, and that time is
/// compared within 20ms (the state's prediction right after a cast does not include the faster rate yet).
/// </summary>
public class CooldownDurationComplex
{
    private const double Tolerance = 0.002;

    private static double ReadyAt(StepDriver f, Ability ability) =>
        f.Now.TotalSeconds + f.Hero.GetCooldownRemains(ability.Name, f.Now);

    private static void AssertTime(double expected, double actual, double tolerance, string what) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected:F3}s, got {actual:F3}s");

    /// <summary>Advances the fight 10ms at a time until the ability is ready; returns that time.</summary>
    private static double WaitUntilReady(StepDriver f, Ability ability)
    {
        while (!f.Hero.IsCooldownReady(ability.Name, f.Now) && f.Now.TotalSeconds < 120.0) f.Wait(0.01);
        return f.Now.TotalSeconds;
    }

    // ---- Reductions add up

    /// <summary>
    /// Player: the time taken off a cooldown by different effects adds up. Flight of the Navir loses 5 seconds when Harrowing
    /// Ice reaches 31 stacks and 0.3 seconds for each orb spent with Wisdom of the North.
    /// Dev: Flight of the Navir cast at the start (ready at 60s); Harrowing Ice set to 30 stacks so the first pulse of the next
    /// Bursting Ice makes the 31st; then a Glacial Blast (2 orbs). Flight of the Navir is ready 5 + 0.6 = 5.6s sooner.
    /// </summary>
    [Fact]
    public void Wisdom_Of_The_North_And_Harrowing_Ice_Reductions_On_Flight_Of_The_Navir_Add_Up()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId, HarrowingIce.TalentId]);
        f.Cast<FlightOfTheNavir>();
        double before = ReadyAt(f, f.Ability<FlightOfTheNavir>());
        f.Hero.Auras.Apply(HarrowingIce.Buff, f.Now, setStacks: HarrowingIce.MaxStacks - 1);

        f.Cast<BurstingIce>();
        f.Wait(1.0); // the first pulse comes 0.5s after the cast
        AssertTime(before - 5.0, ReadyAt(f, f.Ability<FlightOfTheNavir>()), Tolerance, "after Harrowing Ice");
        f.RimeOf().WinterOrbs = 2;
        f.Cast<GlacialBlast>();

        AssertTime(before - 5.0 - 0.6, ReadyAt(f, f.Ability<FlightOfTheNavir>()), Tolerance, "after Wisdom of the North too");
    }

    /// <summary>
    /// Player: with Wisdom of the North and Glacial Assault, a free Talon Strike at 4 stacks fires as if you had 5 orbs and
    /// counts as spending them: 1.5 seconds off the cooldowns, though no orb is spent.
    /// Dev: the three talents Wisdom of the North, Glacial Assault and Icy Talons; Ice Blitz cast at the start; 4 Cold Snaps
    /// (waiting for charges), orbs back to 0; one Talon Strike. Ice Blitz is ready 1.5s sooner and the orbs are still 0.
    /// </summary>
    [Fact]
    public void A_Free_Glacial_Assault_Talon_Strike_Counts_As_5_Orbs_Spent_For_Wisdom_Of_The_North()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId, GlacialAssault.TalentId, IcyTalons.TalentId]);
        f.Cast<IceBlitz>();
        double before = ReadyAt(f, f.Ability<IceBlitz>());
        for (int casts = 0; casts < GlacialAssault.MaxStacks;)
        {
            if (f.TryCast<ColdSnap>().Succeeded) casts++;
            else f.Wait(0.5);
        }
        f.RimeOf().WinterOrbs = 0;

        f.Cast<TalonStrike>();

        AssertTime(before - 1.5, ReadyAt(f, f.Ability<IceBlitz>()), Tolerance, "Ice Blitz");
        Assert.Equal(0, f.RimeOf().WinterOrbs);
    }

    /// <summary>
    /// Player: orbs that a Spirit Refund gives back do not take more time off with Wisdom of the North: only the spend counts.
    /// Dev: Wisdom of the North, 20% Spirit and the refund forced on; Ice Blitz on cooldown; a Glacial Blast from 3 orbs. 0.3s
    /// later the orbs are back (3) and Ice Blitz is ready only 0.6s sooner.
    /// </summary>
    [Fact]
    public void Orbs_Refunded_By_Spirit_Do_Not_Reduce_Cooldowns_Again()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId], spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Cast<IceBlitz>();
        double before = ReadyAt(f, f.Ability<IceBlitz>());
        f.RimeOf().WinterOrbs = 3;

        f.Cast<GlacialBlast>();
        f.Wait(0.3);

        Assert.Equal(3, f.RimeOf().WinterOrbs);
        AssertTime(before - 0.6, ReadyAt(f, f.Ability<IceBlitz>()), Tolerance, "Ice Blitz");
    }

    // ---- Cooldown reduction stat (what gems and the like give)

    /// <summary>
    /// Player: a cooldown reduction, like the one gems give, shortens every cooldown by that fraction when the ability is
    /// used. With 10% reduction and 20% acceleration, Bursting Ice's 10 seconds become 10 x 0.9 / 1.2 seconds.
    /// Dev: the hero's cooldown reduction set to 10% (the stat the gems feed) and one acceleration of 0.2. Checks Ice Blitz
    /// (60s becomes 54s of cooldown, shortened by the acceleration) and Bursting Ice, and that the reduction alone does exactly
    /// 10%. The sources themselves (gems, glyphs, the weapon) are not tested here.
    /// TODO: this is artificial (a round 10% and a made-up acceleration). Use the green gem rank 10 effect instead (the
    /// Emerald "Blessing of the Commander - II", 12% cooldown reduction in the code; to confirm) once gems can be imported.
    /// </summary>
    [Fact]
    public void Cooldown_Reduction_Stat_Shortens_Cooldowns_And_Combines_With_Acceleration()
    {
        var alone = StepFights.Rime();
        alone.Hero.Stats.CooldownReductionPct = 0.1;
        alone.Cast<IceBlitz>();
        AssertTime(60.0 * 0.9, ReadyAt(alone, alone.Ability<IceBlitz>()), Tolerance, "Ice Blitz with 10% reduction");

        var f = StepFights.Rime();
        f.Hero.Stats.CooldownReductionPct = 0.1;
        f.Hero.CooldownAccelerations.Add((_, _) => 0.2);
        f.Cast<BurstingIce>();
        double castEnd = f.Now.TotalSeconds;
        AssertTime(castEnd + 10.0 * 0.9 / 1.2, WaitUntilReady(f, f.Ability<BurstingIce>()), 0.02, "Bursting Ice");
    }
}
