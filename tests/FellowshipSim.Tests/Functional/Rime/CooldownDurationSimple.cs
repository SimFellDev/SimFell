using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Rime's cooldowns last, one effect at a time: the base durations, what haste does to them, and the talents that
/// take time off them. Effects together are in CooldownDurationComplex.cs.
/// Dev: the time an ability is ready again ("ready at") is read from the hero's cooldown state right after the cast
/// (now + time remaining), so a cooldown that is shortened shows as an earlier ready time. Times are compared within 2ms
/// because the engine rounds to the millisecond.
/// </summary>
public class CooldownDurationSimple
{
    private const double Tolerance = 0.002;

    private static double ReadyAt(StepDriver f, Ability ability) =>
        f.Now.TotalSeconds + f.Hero.GetCooldownRemains(ability.Name, f.Now);

    private static double CompletedAt(StepDriver f, Ability ability) =>
        f.Events.Last(e => e.Kind == StepEventKind.CastCompleted && e.Name == ability.Name).Time.TotalSeconds;

    private static void AssertTime(double expected, double actual, string? what = null) =>
        Assert.True(Math.Abs(expected - actual) <= Tolerance, $"{what}: expected {expected:F3}s, got {actual:F3}s");

    private static double Seconds(SimTime t) => t.TotalSeconds;

    /// <summary>The three major abilities, put on cooldown at the start of the fight (Ice Blitz and Winter's Blessing do not
    /// use the global cooldown); returns the time each is ready.</summary>
    private static Dictionary<string, double> MajorsOnCooldown(StepDriver f)
    {
        f.Cast<IceBlitz>();
        f.Cast<WintersBlessing>();
        f.Cast<FlightOfTheNavir>();
        return MajorsReadyAt(f);
    }

    private static Dictionary<string, double> MajorsReadyAt(StepDriver f) => new()
    {
        ["Ice Blitz"] = ReadyAt(f, f.Ability<IceBlitz>()),
        ["Winter's Blessing"] = ReadyAt(f, f.Ability<WintersBlessing>()),
        ["Flight of the Navir"] = ReadyAt(f, f.Ability<FlightOfTheNavir>()),
    };

    private static void AssertMajorsShortenedBy(Dictionary<string, double> before, StepDriver f, double seconds)
    {
        var after = MajorsReadyAt(f);
        foreach (var name in before.Keys) AssertTime(before[name] - seconds, after[name], name);
    }

    // ---- Base durations and haste

    /// <summary>
    /// Player: Bursting Ice has a 10 second cooldown, Freezing Torrent 15 seconds, and Ice Blitz, Winter's Blessing and
    /// Flight of the Navir 60 seconds, counted from the moment the ability takes effect.
    /// Dev: casts each ability and checks it is ready again its cooldown after its cast completed (Bursting Ice takes effect
    /// at the end of its 2s cast).
    /// </summary>
    [Theory]
    [InlineData("Bursting Ice", 10.0)]
    [InlineData("Freezing Torrent", 15.0)]
    [InlineData("Ice Blitz", 60.0)]
    [InlineData("Winter's Blessing", 60.0)]
    [InlineData("Flight of the Navir", 60.0)]
    public void Abilities_Are_Ready_Again_After_Their_Base_Cooldown(string name, double cooldown)
    {
        var f = StepFights.Rime();
        var ability = f.Hero.GetAbility(name)!;

        f.Cast(ability);

        AssertTime(CompletedAt(f, ability) + cooldown, ReadyAt(f, ability), name);
    }

    /// <summary>
    /// Player: haste does not shorten the cooldowns of Bursting Ice, Freezing Torrent, Ice Blitz, Winter's Blessing and
    /// Flight of the Navir.
    /// Dev: the same check at 40% haste; the cooldown still lasts the base time from the end of the cast (which is itself
    /// hasted for Bursting Ice).
    /// </summary>
    [Theory]
    [InlineData("Bursting Ice", 10.0)]
    [InlineData("Freezing Torrent", 15.0)]
    [InlineData("Ice Blitz", 60.0)]
    [InlineData("Winter's Blessing", 60.0)]
    [InlineData("Flight of the Navir", 60.0)]
    public void Haste_Does_Not_Shorten_These_Cooldowns(string name, double cooldown)
    {
        var f = StepFights.Rime(haste: 0.4);
        var ability = f.Hero.GetAbility(name)!;

        f.Cast(ability);

        AssertTime(CompletedAt(f, ability) + cooldown, ReadyAt(f, ability), name);
    }

    // ---- Cold Snap's charges

    /// <summary>
    /// Player: a Cold Snap charge takes 12 seconds to come back, and haste speeds it up: 12 divided by (1 + haste).
    /// Dev: one Cold Snap at the start of the fight at 0%, 20% and 40% haste; the fight is advanced 10ms at a time until
    /// both charges are back, and that time is compared with 12 / (1 + haste), within 20ms. (The time the cooldown state
    /// predicts right after the cast is not used: it does not include the haste yet.)
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Cold_Snap_Charge_Recharges_In_12_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);
        var coldSnap = f.Hero.CooldownOf(f.Ability<ColdSnap>());
        f.Cast<ColdSnap>();

        while (coldSnap.ChargesAt(f.Now) < 2 && f.Now.TotalSeconds < 20.0) f.Wait(0.01);

        double expected = 12.0 / (1 + haste);
        Assert.True(Math.Abs(f.Now.TotalSeconds - expected) <= 0.02, $"expected {expected:F3}s, got {f.Now.TotalSeconds:F3}s");
    }

    /// <summary>
    /// Player: Cold Snap's charges come back one at a time: after using both, the first returns after 12 seconds and the
    /// second 12 seconds after that.
    /// Dev: two Cold Snaps back to back (no haste); checks the charges at 11.9s (0), 12.1s (1), 23.9s (1) and 24.1s (2).
    /// </summary>
    [Fact]
    public void Cold_Snap_Charges_Recharge_One_At_A_Time()
    {
        var f = StepFights.Rime();
        var coldSnap = f.Hero.CooldownOf(f.Ability<ColdSnap>());
        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();

        f.Wait(11.9 - f.Now.TotalSeconds);
        Assert.Equal(0, coldSnap.ChargesAt(f.Now));
        f.Wait(0.2);
        Assert.Equal(1, coldSnap.ChargesAt(f.Now));
        f.Wait(23.9 - f.Now.TotalSeconds);
        Assert.Equal(1, coldSnap.ChargesAt(f.Now));
        f.Wait(0.2);
        Assert.Equal(2, coldSnap.ChargesAt(f.Now));
    }

    // ---- Wisdom of the North: 0.3s off Ice Blitz, Flight of the Navir and Winter's Blessing per Winter Orb spent or lost

    /// <summary>
    /// Player: with Wisdom of the North, each Winter Orb you spend takes 0.3 seconds off the cooldowns of Ice Blitz, Flight
    /// of the Navir and Winter's Blessing.
    /// Dev: the three are put on cooldown at the start; then a Glacial Blast or an Ice Comet (2 orbs, so 0.6s). With Icy
    /// Talons, a Talon Strike or a Rising Talons spends all the orbs, 1 to 5, so 0.3s each.
    /// </summary>
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
        var before = MajorsOnCooldown(f);
        f.RimeOf().WinterOrbs = orbs;

        f.Cast(f.Hero.GetAbility(spender)!);

        AssertMajorsShortenedBy(before, f, 0.3 * orbs);
    }

    /// <summary>
    /// Player: an orb that is lost because you are at the maximum of 5 also takes 0.3 seconds off.
    /// Dev: the three majors on cooldown, orbs set to 5, then a Cold Snap (+1 orb, which overflows): they come back 0.3s
    /// sooner.
    /// </summary>
    [Fact]
    public void Wisdom_Of_The_North_Takes_0_3_Seconds_Off_For_An_Orb_Lost_Over_The_Maximum()
    {
        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId]);
        var before = MajorsOnCooldown(f);
        f.RimeOf().WinterOrbs = 5;

        f.Cast<ColdSnap>();

        AssertMajorsShortenedBy(before, f, 0.3);
    }

    // ---- Chilling Finesse

    /// <summary>
    /// Player: with Chilling Finesse, each tick of Freezing Torrent takes 0.2 seconds off the cooldown of Bursting Ice.
    /// Dev: Bursting Ice cast first (so it is on cooldown), then a Freezing Torrent at 0% and 50% haste (6 and 9 ticks, read
    /// from its damage events); Bursting Ice is ready 0.2s x ticks sooner.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Chilling_Finesse_Takes_0_2_Seconds_Off_Bursting_Ice_Per_Torrent_Tick(double haste)
    {
        var f = StepFights.Rime(haste, talents: [ChillingFinesse.TalentId]);
        f.Cast<BurstingIce>();
        double before = ReadyAt(f, f.Ability<BurstingIce>());

        f.Cast<FreezingTorrent>();

        int ticks = f.Damage("Freezing Torrent").Count;
        Assert.True(ticks > 0);
        AssertTime(before - 0.2 * ticks, ReadyAt(f, f.Ability<BurstingIce>()));
    }

    /// <summary>
    /// Player: with Chilling Finesse, Cold Snap takes 1.5 seconds off the cooldown of Freezing Torrent.
    /// Dev: Freezing Torrent cast first (on cooldown), then a Cold Snap; Freezing Torrent is ready 1.5s sooner.
    /// </summary>
    [Fact]
    public void Chilling_Finesse_Takes_1_5_Seconds_Off_Freezing_Torrent_When_Cold_Snap_Is_Cast()
    {
        var f = StepFights.Rime(talents: [ChillingFinesse.TalentId]);
        f.Cast<FreezingTorrent>();
        double before = ReadyAt(f, f.Ability<FreezingTorrent>());

        f.Cast<ColdSnap>();

        AssertTime(before - 1.5, ReadyAt(f, f.Ability<FreezingTorrent>()));
    }

    // ---- Reductions on cooldowns that are not running

    /// <summary>
    /// Player: shortening a cooldown that is already over does nothing: it does not save time for later. Shortening one by
    /// more than what is left just makes it ready.
    /// Dev: Wisdom of the North. (1) Orbs spent while Ice Blitz was never cast: its later cast still has the full 60s.
    /// (2) Ice Blitz cast at the start, 0.2s left of its cooldown at 59.8s, then a spender takes 0.6s off: it is ready (no
    /// negative remainder), and cast then it has the full 60s again.
    /// </summary>
    [Fact]
    public void A_Reduction_Does_Not_Bank_Time_And_Never_Goes_Below_Zero()
    {
        var unused = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId]);
        unused.RimeOf().WinterOrbs = 2;
        unused.Cast<GlacialBlast>();
        double spentAt = unused.Now.TotalSeconds;
        unused.Cast<IceBlitz>();
        AssertTime(spentAt + 60.0, ReadyAt(unused, unused.Ability<IceBlitz>()), "full cooldown after an early reduction");

        var f = StepFights.Rime(talents: [WisdomOfTheNorth.TalentId]);
        f.Cast<IceBlitz>();
        f.Wait(59.8 - f.Now.TotalSeconds);
        Assert.InRange(f.Hero.GetCooldownRemains<IceBlitz>(f.Now), 0.15, 0.25);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<GlacialBlast>();

        Assert.Equal(0.0, f.Hero.GetCooldownRemains<IceBlitz>(f.Now));
        Assert.True(f.Hero.IsCooldownReady<IceBlitz>(f.Now));
        f.Cast<IceBlitz>();
        AssertTime(f.Now.TotalSeconds + 60.0, ReadyAt(f, f.Ability<IceBlitz>()), "full cooldown after being ready");
    }
}
