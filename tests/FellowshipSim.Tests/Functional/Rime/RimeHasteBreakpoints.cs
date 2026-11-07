using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits Rime's damage over time and channel deal at each haste. Haste makes them tick faster without making them last
/// longer, so the number of hits goes up in steps (breakpoints) rather than smoothly. Every test casts once on a single enemy and
/// counts the hits against the formula in the test's description, written out in the test.
/// </summary>
public class RimeHasteBreakpoints
{
    private const string UnderOneFrame = "To check in game: the engine counts a last partial tick shorter than one server frame";

    /// <summary>
    /// How many full ticks of <paramref name="tick"/> seconds fit in <paramref name="duration"/>. Rounded before the floor: in floating
    /// point 2.8 / 0.4 is 6.999999999999999, which is 7 ticks.
    /// </summary>
    private static int FullTicks(double duration, double tick) => (int)Math.Floor(duration / tick + 1e-9);

    /// <summary>1 when at least <paramref name="threshold"/> seconds are left after the full ticks, for a last partial tick; else 0.</summary>
    private static int PartialTick(double duration, double tick, double threshold) =>
        duration - FullTicks(duration, tick) * tick >= threshold - 1e-9 ? 1 : 0;

    /// <summary>
    /// Bursting Ice ticks every 0.5 seconds for 3 seconds, faster with haste. It hits once per full tick that fits in the 3 seconds,
    /// floor(3 / 0.5 x (1 + haste)), plus a last partial hit when at least 0.1 seconds are left after the last full tick.
    ///
    /// The 0.1 seconds (3 server frames) is the engine's value, fitted to combat logs.
    /// </summary>
    [Theory]
    [InlineData(0.00)]
    [InlineData(0.05)]
    [InlineData(0.10)]
    [InlineData(0.15)]
    [InlineData(0.20)]
    [InlineData(0.25)]
    [InlineData(0.30)]
    [InlineData(0.35)]
    [InlineData(0.40)]
    [InlineData(0.45)]
    [InlineData(0.50)]
    public void Bursting_Ice_Hits_Once_Per_Full_Tick_Plus_A_Partial_Over_0_1_Seconds(double haste)
    {
        double tick = 0.5 / (1 + haste);
        int hits = FullTicks(3.0, tick) + PartialTick(3.0, tick, 0.1);
        var f = StepFights.Rime(haste: haste);

        f.Cast<BurstingIce>();
        f.Wait(4.0); // the 3 second debuff runs out

        Assert.Equal(hits, f.Damage<BurstingIce>().Count);
    }

    /// <summary>
    /// Freezing Torrent ticks every 0.4 seconds, faster with haste, and once more as soon as you start channelling. It hits once per
    /// full tick that fits in the channel, plus that first tick, plus a last partial tick when at least one server frame (1/30
    /// second) is left after the last full tick. Supreme Torrent makes the channel 2.8 seconds instead of 2; Soulfrost Torrent makes
    /// it tick 1.4 times faster.
    ///
    /// TODO: check in game whether a partial tick happens when less than one server frame is left after the last full tick, as in the
    /// skipped rows. The engine counts one there.
    /// </summary>
    [Theory]
    [InlineData(false, false, 0.00)]
    [InlineData(false, false, 0.05)]
    [InlineData(false, false, 0.10)]
    [InlineData(false, false, 0.15)]
    [InlineData(false, false, 0.20)]
    [InlineData(false, false, 0.25)]
    [InlineData(false, false, 0.30)]
    [InlineData(false, false, 0.35)]
    [InlineData(false, false, 0.40)]
    [InlineData(false, false, 0.45)]
    [InlineData(false, false, 0.50)]
    [InlineData(false, false, 0.75)]
    [InlineData(false, false, 1.00)]
    [InlineData(true, false, 0.00)]
    [InlineData(true, false, 0.05)]
    [InlineData(true, false, 0.10)]
    [InlineData(true, false, 0.15, Skip = UnderOneFrame)]
    [InlineData(true, false, 0.20)]
    [InlineData(true, false, 0.25)]
    [InlineData(true, false, 0.30, Skip = UnderOneFrame)]
    [InlineData(true, false, 0.35)]
    [InlineData(true, false, 0.40)]
    [InlineData(true, false, 0.45)]
    [InlineData(true, false, 0.50)]
    [InlineData(true, false, 0.75)]
    [InlineData(true, false, 1.00)]
    [InlineData(false, true, 0.00)]
    [InlineData(false, true, 0.05)]
    [InlineData(false, true, 0.10)]
    [InlineData(false, true, 0.15, Skip = UnderOneFrame)]
    [InlineData(false, true, 0.20)]
    [InlineData(false, true, 0.25)]
    [InlineData(false, true, 0.30, Skip = UnderOneFrame)]
    [InlineData(false, true, 0.35)]
    [InlineData(false, true, 0.40)]
    [InlineData(false, true, 0.45, Skip = UnderOneFrame)]
    [InlineData(false, true, 0.50)]
    [InlineData(false, true, 0.75)]
    [InlineData(false, true, 1.00)]
    [InlineData(true, true, 0.00)]
    [InlineData(true, true, 0.05)]
    [InlineData(true, true, 0.10)]
    [InlineData(true, true, 0.15)]
    [InlineData(true, true, 0.20)]
    [InlineData(true, true, 0.25)]
    [InlineData(true, true, 0.30)]
    [InlineData(true, true, 0.35)]
    [InlineData(true, true, 0.40)]
    [InlineData(true, true, 0.45)]
    [InlineData(true, true, 0.50)]
    [InlineData(true, true, 0.75, Skip = UnderOneFrame)]
    [InlineData(true, true, 1.00)]
    public void Freezing_Torrent_Hits_On_Cast_Once_Per_Full_Tick_Plus_A_Partial_Over_One_Server_Frame(bool supreme, bool soulfrost,
        double haste)
    {
        double channel = supreme ? 2.8 : 2.0;
        double tick = 0.4 / (soulfrost ? 1.4 : 1.0) / (1 + haste);
        int hits = 1 + FullTicks(channel, tick) + PartialTick(channel, tick, 1.0 / 30); // the tick on cast, the full ticks, a partial one
        var talents = new List<int>();
        if (supreme) talents.Add(SupremeTorrent.TalentId);
        if (soulfrost) talents.Add(SoulfrostTorrent.TalentId);
        var f = StepFights.Rime(haste: haste, talents: [.. talents]);
        if (soulfrost) f.Apply(SoulfrostTorrent.Buff); // the faster torrent, without waiting for it to trigger

        f.Cast<FreezingTorrent>();
        f.Wait(1.0); // the last tick lands

        Assert.Equal(hits, f.Damage<FreezingTorrent>().Count);
    }
}
