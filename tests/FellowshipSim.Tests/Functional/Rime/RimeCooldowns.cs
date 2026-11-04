using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Rime's cooldowns last: the base durations, what haste does to them, and the effects that take time off them, on their own
/// and together. How any reduction works (on a ready cooldown, past zero, with charges, with faster recovery) is in CooldownStateTests.cs.
///
/// Every duration is checked by the ability being not ready 0.1s before its end and ready 0.1s after.
/// </summary>
public class RimeCooldowns
{
    private static readonly string[] Majors = ["Ice Blitz", "Winter's Blessing", "Flight of the Navir"];

    // ---- Base durations and haste

    /// <summary>
    /// Bursting Ice has a 10 second cooldown, Freezing Torrent 15 seconds, and Ice Blitz, Winter's Blessing and Flight of the Navir
    /// 60 seconds, counted from the end of the cast. Haste does not shorten them.
    /// </summary>
    [Theory]
    [InlineData("Bursting Ice", 10.0, 0.0)]
    [InlineData("Bursting Ice", 10.0, 0.4)]
    [InlineData("Freezing Torrent", 15.0, 0.0)]
    [InlineData("Freezing Torrent", 15.0, 0.4)]
    [InlineData("Ice Blitz", 60.0, 0.0)]
    [InlineData("Ice Blitz", 60.0, 0.4)]
    [InlineData("Winter's Blessing", 60.0, 0.0)]
    [InlineData("Winter's Blessing", 60.0, 0.4)]
    [InlineData("Flight of the Navir", 60.0, 0.0)]
    [InlineData("Flight of the Navir", 60.0, 0.4)]
    public void Cooldowns_Other_Than_Cold_Snap_Are_Not_Hasted(string ability, double cooldown, double haste)
    {
        var f = StepFights.Rime(haste: haste);

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    // ---- Cold Snap's charges

    /// <summary>A Cold Snap charge takes 12 seconds to come back, and sped up by haste: 12 / (1 + haste).</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Cold_Snap_Has_A_Hasted_Cooldown(double haste)
    {
        var f = StepFights.Rime(haste: haste);

        f.Cast<ColdSnap>();
        double end = f.CompletedAt("Cold Snap") + 12.0 / (1 + haste);

        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<ColdSnap>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<ColdSnap>());
    }

    /// <summary>Cold Snap's charges come back one at a time: after using both, the first returns after 12 seconds, the second 12 seconds later.</summary>
    [Fact]
    public void Cold_Snap_Charges_Come_Back_One_At_A_Time()
    {
        var f = StepFights.Rime();

        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();

        using var check = new Checks(f);
        f.WaitUntil(11.9);
        check.Equal(0, f.Charges<ColdSnap>());
        f.WaitUntil(12.1);
        check.Equal(1, f.Charges<ColdSnap>());
        f.WaitUntil(23.9);
        check.Equal(1, f.Charges<ColdSnap>());
        f.WaitUntil(24.1);
        check.Equal(2, f.Charges<ColdSnap>());
    }

    /// <summary>
    /// Blessing of the Commander's cooldown reduction and haste combine on Cold Snap's charges: with the rank 10 node (12%), a charge
    /// comes back in 12 x (1 - 0.12) / (1 + haste) seconds.
    ///
    /// The Emerald gem's power unlocks every node up to rank 10. Blessing of the Commander on its own belongs in a gear test over every hero.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    public void Blessing_Of_The_Commander_And_Haste_Combine_On_Cold_Snap(double haste)
    {
        var f = StepFights.Rime(haste: haste, gemPower: new() { [GemColor.Emerald] = 1500 }); // rank 10: Blessing of the Commander - II

        f.Cast<ColdSnap>();
        double end = f.CompletedAt("Cold Snap") + 12.0 * (1 - 0.12) / (1 + haste);

        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<ColdSnap>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<ColdSnap>());
    }

    // ---- Wisdom of the North: 0.3s off Ice Blitz, Winter's Blessing and Flight of the Navir per Winter Orb spent or lost

    /// <summary>With Wisdom of the North, each Winter Orb you spend takes 0.3 seconds off the cooldowns of Ice Blitz, Winter's Blessing and Flight of the Navir.</summary>
    [Theory]
    [InlineData("Glacial Blast", 2, false)]
    [InlineData("Ice Comet", 2, false)]
    [InlineData("Talon Strike", 1, true)]
    [InlineData("Talon Strike", 3, true)]
    [InlineData("Talon Strike", 5, true)]
    [InlineData("Rising Talons", 1, true)]
    [InlineData("Rising Talons", 3, true)]
    [InlineData("Rising Talons", 5, true)]
    public void Wisdom_Of_The_North_Takes_0_3_Seconds_Off_Per_Orb_Spent(string spender, int orbs, bool icyTalons)
    {
        var f = StepFights.Rime(talents: icyTalons ? [WisdomOfTheNorth.TalentId, IcyTalons.TalentId] : [WisdomOfTheNorth.TalentId]);

        foreach (var major in Majors) f.Cast(major); // all three at 0s: none waits for the global cooldown
        f.RimeOf().WinterOrbs = orbs;

        f.Cast(spender);
        double end = 60.0 - 0.3 * orbs;

        using var check = new Checks(f);
        f.WaitUntil(end - 0.1);
        foreach (var major in Majors) check.False(f.IsReady(major), $"{major} ready");
        f.WaitUntil(end + 0.1);
        foreach (var major in Majors) check.True(f.IsReady(major), $"{major} ready");
    }

    /// <summary>With Wisdom of the North, a Winter Orb lost because you are at the maximum of 5 also takes 0.3 seconds off.</summary>
    [Fact]
    public void Wisdom_Of_The_North_Takes_0_3_Seconds_Off_For_An_Orb_Lost_Over_The_Maximum()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId]);

        foreach (var major in Majors) f.Cast(major);
        f.RimeOf().WinterOrbs = 5;

        f.Cast<ColdSnap>(); // its orb is lost
        double end = 60.0 - 0.3;

        using var check = new Checks(f);
        f.WaitUntil(end - 0.1);
        foreach (var major in Majors) check.False(f.IsReady(major), $"{major} ready");
        f.WaitUntil(end + 0.1);
        foreach (var major in Majors) check.True(f.IsReady(major), $"{major} ready");
    }

    /// <summary>
    /// Time taken off a cooldown by different effects adds up: Flight of the Navir loses 5 seconds when Harrowing Ice reaches 31
    /// stacks, and 0.3 seconds per orb spent with Wisdom of the North.
    /// </summary>
    [Fact]
    public void Wisdom_Of_The_North_And_Harrowing_Ice_Reductions_On_Flight_Of_The_Navir_Add_Up()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId, HarrowingIce.TalentId]);

        f.Cast<FlightOfTheNavir>();
        f.Apply(HarrowingIce.Buff, stacks: 30); // the next Bursting Ice pulse makes the 31st

        f.Cast<BurstingIce>();
        f.Wait(1.0); // the first pulse lands
        f.RimeOf().WinterOrbs = 2;
        f.Cast<GlacialBlast>();
        double end = f.CompletedAt("Flight of the Navir") + 60.0 - 5.0 - 0.3 * 2;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<FlightOfTheNavir>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<FlightOfTheNavir>());
    }

    /// <summary>
    /// With Wisdom of the North, Glacial Assault and Icy Talons, the free Talon Strike at 4 stacks fires as if you had 5 orbs, and counts
    /// as spending them: 1.5 seconds off the cooldowns, though no orb is spent.
    /// </summary>
    [Fact]
    public void A_Free_Glacial_Assault_Talon_Strike_Counts_As_5_Orbs_Spent_For_Wisdom_Of_The_North()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId, GlacialAssault.TalentId, IcyTalons.TalentId]);

        f.Cast<IceBlitz>();
        f.Apply(GlacialAssault.Buff, stacks: 4);

        f.Cast<TalonStrike>();
        double end = f.CompletedAt("Ice Blitz") + 60.0 - 0.3 * 5;

        using var check = new Checks(f);
        check.Equal(0, f.RimeOf().WinterOrbs);
        f.WaitUntil(end - 0.1);
        check.False(f.IsReady<IceBlitz>());
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady<IceBlitz>());
    }

    /// <summary>With Wisdom of the North, orbs a Spirit Refund gives back take no more time off: only the spend counts.</summary>
    [Fact]
    public void Orbs_Refunded_By_Spirit_Do_Not_Reduce_Cooldowns_Again()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId], spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.Cast<IceBlitz>();
        f.RimeOf().WinterOrbs = 3;

        f.Cast<GlacialBlast>(); // spends 2
        f.Wait(0.3); // the refund lands
        double end = f.CompletedAt("Ice Blitz") + 60.0 - 0.3 * 2;

        using var check = new Checks(f);
        check.Equal(3, f.RimeOf().WinterOrbs);
        f.WaitUntil(end - 0.1);
        check.False(f.IsReady<IceBlitz>());
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady<IceBlitz>());
    }

    // ---- Chilling Finesse

    /// <summary>With Chilling Finesse, each tick of Freezing Torrent takes 0.2 seconds off the cooldown of Bursting Ice.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Chilling_Finesse_Takes_0_2_Seconds_Off_Bursting_Ice_Per_Torrent_Tick(double haste)
    {
        var f = StepFights.Rime(haste: haste, talents: [ChillingFinesse.TalentId]);

        f.Cast<BurstingIce>();

        f.Cast<FreezingTorrent>();
        int ticks = f.Damage<FreezingTorrent>().Count; // more with haste
        double end = f.CompletedAt("Bursting Ice") + 10.0 - 0.2 * ticks;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<BurstingIce>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<BurstingIce>());
    }

    /// <summary>With Chilling Finesse, Cold Snap takes 1.5 seconds off the cooldown of Freezing Torrent.</summary>
    [Fact]
    public void Chilling_Finesse_Takes_1_5_Seconds_Off_Freezing_Torrent_When_Cold_Snap_Is_Cast()
    {
        var f = StepFights.Rime(talents: [ChillingFinesse.TalentId]);

        f.Cast<FreezingTorrent>();

        f.Cast<ColdSnap>();
        double end = f.CompletedAt("Freezing Torrent") + 15.0 - 1.5;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<FreezingTorrent>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<FreezingTorrent>());
    }
}
