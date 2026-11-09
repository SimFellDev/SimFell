using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Winter's Embrace: when the Bursting Ice cast ends, you deal 20% more damage for 3 seconds. These tests are about which hits
/// get the bonus near the edges of that window, and how much haste it takes to fit casts into it.
///
/// Bursting Ice is a 2 second cast at the start of the fight, so the window runs from 2.0s to 5.0s (the engine ends it on the
/// next server frame, about 5.017s); the times tested stay clear of its end unless the edge is the point. A hit is buffed when it
/// deals 1.2 times the same hit in a fight without Bursting Ice.
/// </summary>
public class RimeWintersEmbrace
{
    // ---- When a hit is buffed

    /// <summary>
    /// A cast is buffed when it ends inside the window, wherever it started: a Frost Bolt or Glacial Blast (1.5 second casts) that
    /// ends at 4.9s is buffed, one that starts inside the window but ends at 5.1s is not. Ice Comet is instant, so it ends when cast.
    /// </summary>
    [Theory]
    [InlineData("Frost Bolt", 3.0, true)] // ends at 4.5s
    [InlineData("Frost Bolt", 3.4, true)] // ends at 4.9s
    [InlineData("Frost Bolt", 3.6, false)] // ends at 5.1s
    [InlineData("Frost Bolt", 4.0, false)] // ends at 5.5s
    [InlineData("Glacial Blast", 3.0, true)]
    [InlineData("Glacial Blast", 3.4, true)]
    [InlineData("Glacial Blast", 3.6, false)]
    [InlineData("Glacial Blast", 4.0, false)]
    [InlineData("Ice Comet", 2.0, true)]
    [InlineData("Ice Comet", 5.2, false)]
    public void A_Cast_Is_Buffed_When_It_Ends_Inside_The_Window(string ability, double castAt, bool buffed)
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();
        plainScenario.RimeOf().WinterOrbs = 2; // enough for Glacial Blast or Ice Comet

        plainScenario.Cast(ability);
        plainScenario.Wait(4.0); // the comet falls
        double plainDamage = plainScenario.Damage(ability).First().Value;

        var embraceScenario = StepFights.Rime().WithoutDamageSpread();
        embraceScenario.RimeOf().WinterOrbs = 2;

        embraceScenario.Cast<BurstingIce>();
        embraceScenario.WaitUntil(castAt);

        embraceScenario.Cast(ability);
        embraceScenario.Wait(4.0); // the comet falls
        double embraceDamage = embraceScenario.Damage(ability).First().Value;

        AssertRatio(buffed ? 1.2 : 1.0, embraceDamage / plainDamage);
    }

    /// <summary>An Ice Comet cast at the end of the window is buffed, though it lands after the window is over.</summary>
    [Fact]
    public void An_Ice_Comet_Cast_Inside_The_Window_Is_Buffed_Though_It_Lands_After()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();
        plainScenario.RimeOf().WinterOrbs = 2; // enough for Ice Comet

        plainScenario.Cast<IceComet>();
        plainScenario.Wait(4.0); // the comet falls
        double plainCometDamage = plainScenario.Damage<IceComet>().Single().Value;

        var embraceScenario = StepFights.Rime().WithoutDamageSpread();
        embraceScenario.RimeOf().WinterOrbs = 2;

        embraceScenario.Cast<BurstingIce>();
        embraceScenario.WaitUntil(4.8);

        embraceScenario.Cast<IceComet>();
        embraceScenario.Wait(4.0); // the comet falls
        var comet = embraceScenario.Damage<IceComet>().Single();
        var windowEnd = embraceScenario.AurasRemoved(BurstingIce.WintersEmbrace).Single().Time;

        using var check = new Checks(embraceScenario);
        check.True(comet.Time > windowEnd, "the comet lands after the window");
        check.Ratio(1.2, comet.Value / plainCometDamage);
    }

    /// <summary>
    /// Each Freezing Torrent tick is buffed when it happens inside the window. Without haste the torrent ticks 6 times, 0.4 seconds
    /// apart from its start: all 6 are buffed when it starts at 2.0s, the first 3 (up to 4.8s) when it starts at 4.0s, none when it
    /// starts at 5.2s.
    /// </summary>
    [Theory]
    [InlineData(2.0, 6)]
    [InlineData(4.0, 3)]
    [InlineData(5.2, 0)]
    public void Freezing_Torrent_Ticks_Are_Buffed_While_The_Window_Is_Up(double castAt, int buffedTicks)
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast<FreezingTorrent>();
        double plainTickDamage = plainScenario.Damage<FreezingTorrent>().First().Value;

        var embraceScenario = StepFights.Rime().WithoutDamageSpread();

        embraceScenario.Cast<BurstingIce>();
        embraceScenario.WaitUntil(castAt);

        embraceScenario.Cast<FreezingTorrent>();
        var ticks = embraceScenario.Damage<FreezingTorrent>();

        using var check = new Checks(embraceScenario);
        check.Equal(6, ticks.Count, what: "ticks");
        for (int i = 0; i < ticks.Count; i++)
            check.Ratio(i < buffedTicks ? 1.2 : 1.0, ticks[i].Value / plainTickDamage, what: $"tick {i + 1}");
    }

    // ---- Haste to fit casts into the window

    /// <summary>
    /// Instant casts after Bursting Ice follow each other every global cooldown, 1.5 seconds shortened by haste, and each one cast
    /// inside the window is buffed. In the rotation Ice Comet, Ice Comet, Cold Snap, Cold Snap, Ice Comet, the first 3 are buffed
    /// with no haste, the 4th from 50% haste and the 5th from 100%.
    ///
    /// At 50% and 100% the cast lands on the end of the window, 5.0s, and is buffed: the hastes around them are tested too. The
    /// two Cold Snaps give the 2 orbs the last Ice Comet needs.
    /// </summary>
    [Theory]
    [InlineData(0.00, 3)]
    [InlineData(0.25, 3)]
    [InlineData(0.45, 3)]
    [InlineData(0.50, 4)]
    [InlineData(0.55, 4)]
    [InlineData(0.95, 4)]
    [InlineData(1.00, 5)]
    [InlineData(1.05, 5)]
    public void Instant_Casts_Are_Buffed_While_They_Fit_In_The_Window(double haste, int buffedCasts)
    {
        var plainScenario = StepFights.Rime(haste: haste).WithoutDamageSpread();
        plainScenario.RimeOf().WinterOrbs = 2; // enough for Ice Comet

        plainScenario.Cast<IceComet>();
        plainScenario.Cast<ColdSnap>();
        plainScenario.Wait(4.0); // the comet falls
        double plainCometDamage = plainScenario.Damage<IceComet>().Single().Value;
        double plainSnapDamage = plainScenario.Damage<ColdSnap>().Single().Value;

        var embraceScenario = StepFights.Rime(haste: haste).WithoutDamageSpread();
        embraceScenario.RimeOf().WinterOrbs = 4; // enough for the first 2 Ice Comets

        embraceScenario.Cast<BurstingIce>();

        embraceScenario.Cast<IceComet>();
        embraceScenario.Cast<IceComet>();
        embraceScenario.Cast<ColdSnap>();
        embraceScenario.Cast<ColdSnap>();
        embraceScenario.Cast<IceComet>();
        embraceScenario.Wait(4.0); // the comets fall
        var comets = embraceScenario.Damage<IceComet>();
        var snaps = embraceScenario.Damage<ColdSnap>();
        double[] ratios =
        [
            comets[0].Value / plainCometDamage,
            comets[1].Value / plainCometDamage,
            snaps[0].Value / plainSnapDamage,
            snaps[1].Value / plainSnapDamage,
            comets[2].Value / plainCometDamage,
        ];

        using var check = new Checks(embraceScenario);
        for (int cast = 0; cast < ratios.Length; cast++)
            check.Ratio(cast < buffedCasts ? 1.2 : 1.0, ratios[cast], what: $"cast {cast + 1}");
    }

    /// <summary>
    /// After Bursting Ice, in the rotation Freezing Torrent, Cold Snap, Cold Snap, every torrent tick is buffed, and so is the first Cold
    /// Snap: the torrent's 2 second channel is not shortened by haste, so it starts 2 seconds into the window. The second Cold Snap starts
    /// one global cooldown later, 2 + 1.5 / (1 + haste) seconds into the window, and is buffed from 50% haste.
    ///
    /// Each Cold Snap is compared with a Cold Snap alone at the same haste, and each torrent tick with the same tick of a torrent alone.
    /// </summary>
    [Theory]
    [InlineData(0.00, false)]
    [InlineData(0.25, false)]
    [InlineData(0.45, false)]
    [InlineData(0.50, true)]
    [InlineData(0.55, true)]
    [InlineData(0.95, true)]
    [InlineData(1.00, true)]
    [InlineData(1.05, true)]
    public void Torrent_Rotation_After_Bursting_Ice_Is_Buffed_While_It_Starts_In_The_Window(double haste, bool secondSnapBuffed)
    {
        var plainTorrentScenario = StepFights.Rime(haste: haste).WithoutDamageSpread();

        plainTorrentScenario.Cast<FreezingTorrent>();
        var plainTicks = plainTorrentScenario.Damage<FreezingTorrent>();

        var plainSnapScenario = StepFights.Rime(haste: haste).WithoutDamageSpread();

        plainSnapScenario.Cast<ColdSnap>();
        double plainSnapDamage = plainSnapScenario.Damage<ColdSnap>().Single().Value;

        var embraceScenario = StepFights.Rime(haste: haste).WithoutDamageSpread();

        embraceScenario.Cast<BurstingIce>();

        embraceScenario.Cast<FreezingTorrent>();
        embraceScenario.Cast<ColdSnap>();
        embraceScenario.Cast<ColdSnap>();
        embraceScenario.Wait(4.0); // every hit lands
        var ticks = embraceScenario.Damage<FreezingTorrent>();
        var snaps = embraceScenario.Damage<ColdSnap>();

        using var check = new Checks(embraceScenario);
        check.Ratio(1.2, snaps[0].Value / plainSnapDamage, what: "first Cold Snap");
        check.Ratio(secondSnapBuffed ? 1.2 : 1.0, snaps[1].Value / plainSnapDamage, what: "second Cold Snap");
        check.Equal(plainTicks.Count, ticks.Count, what: "torrent ticks");
        for (int i = 0; i < Math.Min(ticks.Count, plainTicks.Count); i++)
            check.Ratio(1.2, ticks[i].Value / plainTicks[i].Value, what: $"torrent tick {i + 1}");
    }
}
