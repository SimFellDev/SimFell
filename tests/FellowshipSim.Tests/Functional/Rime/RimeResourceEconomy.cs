using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's Anima and Winter Orbs: what builds them, what spends them, and the talents that change either. What a Spirit Refund gives
/// back is in Generic/SpiritRefund.cs.
/// </summary>
public class RimeResourceEconomy
{
    // ---- Anima and Winter Orbs

    /// <summary>A Frost Bolt gives 3 Anima when its cast completes, not when it starts.</summary>
    [Fact]
    public void Frost_Bolt_Gives_3_Anima_When_The_Cast_Completes()
    {
        var f = StepFights.Rime();
        Assert.Equal(0, f.Amount(Rime.Anima));

        f.Cast<FrostBolt>();

        using var check = new Checks(f);
        check.Equal(3, f.Amount(Rime.Anima));
        check.Seconds(f.CompletedAt("Frost Bolt"), f.Gains("Anima").Single().Time.TotalSeconds);
    }

    /// <summary>Every 9 Anima become a Winter Orb, and your Anima starts again from 0.</summary>
    [Fact]
    public void Nine_Anima_Make_A_Winter_Orb_And_Anima_Starts_Again_From_0()
    {
        var f = StepFights.Rime();

        f.Cast<FrostBolt>();
        f.Cast<FrostBolt>();

        using var check = new Checks(f);
        check.Equal(0, f.RimeOf().WinterOrbs, what: "orbs at 6 Anima");
        f.Cast<FrostBolt>();
        check.Equal(1, f.RimeOf().WinterOrbs);
        check.Equal(0, f.Amount(Rime.Anima));
    }

    /// <summary>Anima past 9 is not lost: you get the orb and keep the rest.</summary>
    [Fact]
    public void Anima_Past_9_Is_Kept_After_The_Orb()
    {
        var f = StepFights.Rime();
        f.Set(Rime.Anima, 8); // Frost Bolts alone only reach 3, 6 and 9

        f.Cast<FrostBolt>(); // 8 + 3 = 11

        using var check = new Checks(f);
        check.Equal(1, f.RimeOf().WinterOrbs);
        check.Equal(2, f.Amount(Rime.Anima));
    }

    /// <summary>Cold Snap gives a Winter Orb.</summary>
    [Fact]
    public void Cold_Snap_Gives_1_Winter_Orb()
    {
        var f = StepFights.Rime();

        f.Cast<ColdSnap>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>You hold at most 5 Winter Orbs: an orb gained at 5 is lost.</summary>
    [Fact]
    public void Winter_Orbs_Stop_At_5()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 5;

        f.Cast<ColdSnap>(); // +1 orb

        using var check = new Checks(f);
        check.Equal(5, f.RimeOf().WinterOrbs);
        check.Equal(1, f.Overflows("Winter Orbs").Count, what: "orbs lost");
    }

    // ---- Spenders

    /// <summary>Glacial Blast and Ice Comet cost 2 Winter Orbs.</summary>
    [Theory]
    [InlineData("Glacial Blast")]
    [InlineData("Ice Comet")]
    public void Glacial_Blast_And_Ice_Comet_Cost_2_Winter_Orbs(string spender)
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 3;

        f.Cast(spender);

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>With a single Winter Orb you cannot cast Glacial Blast or Ice Comet, and you keep the orb.</summary>
    [Theory]
    [InlineData("Glacial Blast")]
    [InlineData("Ice Comet")]
    public void Glacial_Blast_And_Ice_Comet_Cannot_Be_Cast_With_1_Winter_Orb(string spender)
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 1;

        var attempt = f.TryCast(spender);

        using var check = new Checks(f);
        check.False(attempt.Succeeded);
        check.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>With Icy Talons, Talon Strike and Rising Talons use up all your Winter Orbs, and strike once per orb.</summary>
    [Theory]
    [InlineData("Talon Strike", 1)]
    [InlineData("Talon Strike", 2)]
    [InlineData("Talon Strike", 3)]
    [InlineData("Talon Strike", 4)]
    [InlineData("Talon Strike", 5)]
    [InlineData("Rising Talons", 1)]
    [InlineData("Rising Talons", 2)]
    [InlineData("Rising Talons", 3)]
    [InlineData("Rising Talons", 4)]
    [InlineData("Rising Talons", 5)]
    public void Icy_Talons_Spenders_Use_Up_All_Winter_Orbs_And_Strike_Once_Per_Orb(string spender, int orbs)
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);
        f.RimeOf().WinterOrbs = orbs;

        f.Cast(spender);
        f.Wait(5.0); // the spikes and waves land

        using var check = new Checks(f);
        check.Equal(0, f.RimeOf().WinterOrbs);
        check.Equal(orbs, f.Damage(spender).Count, what: "strikes");
    }

    /// <summary>With Icy Talons, you cannot cast Talon Strike or Rising Talons with no Winter Orbs.</summary>
    [Theory]
    [InlineData("Talon Strike")]
    [InlineData("Rising Talons")]
    public void Icy_Talons_Spenders_Cannot_Be_Cast_With_0_Winter_Orbs(string spender)
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);

        Assert.False(f.TryCast(spender).Succeeded);
    }

    // ---- Anima per pulse and per tick

    /// <summary>Bursting Ice gives 1 Anima per pulse, however many enemies the pulse hits, at any haste.</summary>
    [Theory]
    [InlineData(0.0, 1)]
    [InlineData(0.0, 3)]
    [InlineData(0.5, 1)]
    [InlineData(0.5, 3)]
    public void Bursting_Ice_Gives_1_Anima_Per_Pulse_Whatever_The_Number_Of_Enemies(double haste, int enemies)
    {
        var f = StepFights.Rime(haste: haste, enemies: enemies);

        f.Cast<BurstingIce>();
        f.Wait(4.0); // the 3 second debuff runs out

        int pulses = f.Damage<BurstingIce>().Select(hit => hit.Time).Distinct().Count(); // a pulse hits every enemy at once
        var gains = f.Gains("Anima");

        using var check = new Checks(f);
        check.Equal(pulses, gains.Count, what: "Anima gains");
        check.True(gains.All(gain => gain.Value == 1), "every gain is 1 Anima");
    }

    /// <summary>
    /// Every Freezing Torrent tick gives 1 Anima, whatever the torrent (plain, Supreme, Soulfrost or both) and the haste.
    ///
    /// How many ticks each torrent has is in RimeHasteBreakpoints.cs.
    /// </summary>
    [Theory]
    [InlineData(false, false, 0.0)]
    [InlineData(false, false, 0.5)]
    [InlineData(true, false, 0.0)]
    [InlineData(true, false, 0.5)]
    [InlineData(false, true, 0.0)]
    [InlineData(false, true, 0.5)]
    [InlineData(true, true, 0.0)]
    [InlineData(true, true, 0.5)]
    public void Freezing_Torrent_Gives_1_Anima_Per_Tick(bool supreme, bool soulfrost, double haste)
    {
        var talents = new List<int>();
        if (supreme) talents.Add(SupremeTorrent.TalentId);
        if (soulfrost) talents.Add(SoulfrostTorrent.TalentId);
        var f = StepFights.Rime(haste: haste, talents: [.. talents]);
        if (soulfrost) f.Apply(SoulfrostTorrent.Buff); // the faster torrent, without waiting for it to trigger

        f.Cast<FreezingTorrent>();
        f.Wait(1.0); // the last tick lands

        int ticks = f.Damage<FreezingTorrent>().Count;
        var gains = f.Gains("Anima");

        using var check = new Checks(f);
        check.Equal(ticks, gains.Count, what: "Anima gains");
        check.True(gains.All(gain => gain.Value == 1), "every gain is 1 Anima");
    }

    // ---- Winter Orbs over time

    /// <summary>
    /// Wrath of Winter gives a Winter Orb when its cast completes, then one every 4 seconds, shortened by haste, the 30% haste of the
    /// Spirit of Heroism it gives included.
    ///
    /// Only the first 5 orbs are read: at 5, the cap, the next ones are lost.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Wrath_Of_Winter_Gives_A_Winter_Orb_Every_4_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough for Wrath of Winter

        f.Cast<WrathOfWinter>();
        f.Wait(20.0); // Wrath of Winter runs out

        var orbs = f.Gains("Winter Orbs").Take(5).ToList();
        double period = 4.0 / (1 + haste + 0.3); // Spirit of Heroism's 30% haste

        using var check = new Checks(f);
        check.Equal(5, orbs.Count, what: "orbs gained");
        check.Seconds(f.CompletedAt("Wrath of Winter"), orbs[0].Time.TotalSeconds, what: "the first orb's time");
        for (int i = 1; i < orbs.Count; i++)
            check.Seconds(period, (orbs[i].Time - orbs[i - 1].Time).TotalSeconds, what: $"time between orb {i} and orb {i + 1}");
    }

    // ---- Talents

    /// <summary>With Burstbolter, a Frost Bolt gives 1 more Anima when it hits, however many enemies there are.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Burstbolter_Gives_1_Anima_When_The_Frost_Bolt_Hits(int enemies)
    {
        var f = StepFights.Rime(enemies: enemies, talents: [Burstbolter.TalentId]);

        f.Cast<FrostBolt>();
        f.Wait(1.0); // the bolt lands (today as its cast completes; the wait leaves room for a travel time)

        var gains = f.Gains("Anima");

        using var check = new Checks(f);
        check.Equal(2, gains.Count, what: "Anima gains");
        check.Equal(3, gains[0].Value, what: "the cast's Anima");
        check.Seconds(f.CompletedAt("Frost Bolt"), gains[0].Time.TotalSeconds, what: "the cast's Anima time");
        check.Equal(1, gains[1].Value, what: "Burstbolter's Anima");
        check.Seconds(f.Damage<FrostBolt>().Single().Time.TotalSeconds, gains[1].Time.TotalSeconds, what: "Burstbolter's Anima time");
    }

    /// <summary>
    /// With Bursting Swallows, a Frost Swallow that triggers it gives 1 Anima: a Cold Snap's 3 swallows give 3 when they all trigger,
    /// none when none does.
    /// </summary>
    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    public void Bursting_Swallows_Gives_1_Anima_Per_Swallow_That_Triggers_It(bool triggers, int anima)
    {
        var f = StepFights.Rime(talents: [BurstingSwallows.TalentId]);
        f.ForceProcs(key => key == BurstingSwallows.ProcKey ? triggers : null);

        f.Cast<ColdSnap>(); // 1 Winter Orb: 3 Frost Swallows
        f.Wait(3.0); // the swallows land

        using var check = new Checks(f);
        check.Equal(3, f.Damage("Frost Swallows").Count);
        check.Equal(anima, f.Amount(Rime.Anima));
    }
}
