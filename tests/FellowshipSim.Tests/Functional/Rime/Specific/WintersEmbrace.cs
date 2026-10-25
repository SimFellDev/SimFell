using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Winter's Embrace: while Bursting Ice is up on an enemy you deal 20% more damage. The buff starts when the Bursting Ice
/// cast ends and lasts as long as its debuff, 3 seconds (the engine runs it out 17ms later, on the next server frame).
/// These tests are about when exactly a hit gets the damage buff.
/// Dev: Bursting Ice is cast first, so the fight clock is 2.0s when it returns and the buff runs until about 5.017s (the
/// time of its removal is read from the aura events in the checks that need it). A hit is buffed when the damage is 1.2
/// times that of the same hit in a fight without Bursting Ice, within 0.001.
/// </summary>
public class WintersEmbraceTiming
{
    private static void AssertBuffed(double ratio, bool buffed) =>
        Assert.InRange(ratio, (buffed ? 1.2 : 1.0) - 0.001, (buffed ? 1.2 : 1.0) + 0.001);

    private static StepDriver AfterBurstingIce(double waitSeconds)
    {
        var f = StepFights.Rime();
        f.Cast<BurstingIce>();
        f.Wait(waitSeconds);
        return f;
    }

    private static double PlainDamage(string ability, Action<StepDriver> cast)
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 2;
        cast(f);
        f.Wait(4.0);
        return f.Damage(ability).First().Value;
    }

    private static SimTime EmbraceEnd(StepDriver f) =>
        f.Events.First(e => e.Kind == StepEventKind.AuraRemoved && e.Name == "Winter's Embrace").Time;

    /// <summary>
    /// Player: Frost Bolt gets the Winter's Embrace bonus according to when the cast ends: a bolt that finishes while the
    /// buff is up is buffed, even if it started earlier in the window; a bolt that is started inside the window but ends after
    /// it is not buffed.
    /// Dev: Bursting Ice, a wait of 1.0, 1.4, 1.6 or 2.0s, then a Frost Bolt (1.5s cast); the buff ends about 5.02s. The
    /// bolts that end at 4.5s and 4.9s are buffed, those that end at 5.1s and 5.5s are not.
    /// </summary>
    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.4, true)]
    [InlineData(1.6, false)]
    [InlineData(2.0, false)]
    public void Frost_Bolt_Is_Buffed_When_Its_Cast_Ends_In_The_Window(double wait, bool buffed)
    {
        var f = AfterBurstingIce(wait);

        f.Cast<FrostBolt>();

        AssertBuffed(f.Damage("Frost Bolt").First().Value / PlainDamage("Frost Bolt", p => p.Cast<FrostBolt>()), buffed);
    }

    /// <summary>
    /// Player: the same for Glacial Blast: it is buffed when its cast ends inside the window, and not when it ends after.
    /// Dev: as the Frost Bolt test, with 2 orbs set after Bursting Ice and a Glacial Blast (1.5s cast).
    /// </summary>
    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.4, true)]
    [InlineData(1.6, false)]
    [InlineData(2.0, false)]
    public void Glacial_Blast_Is_Buffed_When_Its_Cast_Ends_In_The_Window(double wait, bool buffed)
    {
        var f = AfterBurstingIce(wait);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<GlacialBlast>();

        AssertBuffed(f.Damage("Glacial Blast").First().Value / PlainDamage("Glacial Blast", p => p.Cast<GlacialBlast>()), buffed);
    }

    /// <summary>
    /// Player: Ice Comet is instant, so it is buffed whenever you cast it inside the window, even though the comet lands
    /// much later, after the buff is over. A comet cast after the window is not buffed.
    /// Dev: Bursting Ice, a wait of 0, 2.8 or 3.2s, 2 orbs set, then an Ice Comet. At 2.8s (cast at 4.8s, inside the window)
    /// the comet lands after the buff was removed (checked) and is still buffed; at 3.2s (cast at 5.2s) it is not.
    /// </summary>
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(2.8, true)]
    [InlineData(3.2, false)]
    public void Ice_Comet_Is_Buffed_When_It_Is_Cast_In_The_Window_Whenever_It_Lands(double wait, bool buffed)
    {
        var f = AfterBurstingIce(wait);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<IceComet>();
        f.Wait(4.0);

        var hit = f.Damage("Ice Comet").First();
        if (wait == 2.8) Assert.True(hit.Time > EmbraceEnd(f), "the comet should land after the buff is gone");
        AssertBuffed(hit.Value / PlainDamage("Ice Comet", p => p.Cast<IceComet>()), buffed);
    }

    /// <summary>
    /// Player: for Freezing Torrent, each tick is buffed or not by the time it happens: the ticks during the window are
    /// buffed, the ticks after it are not.
    /// Dev: Bursting Ice, a wait of 0, 2.0 or 3.2s, then a Freezing Torrent (no haste: 6 ticks, 0.4s apart, each hit the same
    /// size without the buff). Every hit is compared with the same hit of a torrent without Bursting Ice: buffed if its time
    /// is before the buff's removal (read from the aura events), not buffed after. At 2.0s the channel straddles the end.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(2.0)]
    [InlineData(3.2)]
    public void Freezing_Torrent_Ticks_Are_Buffed_While_The_Window_Is_Up(double wait)
    {
        var plain = StepFights.Rime();
        plain.Cast<FreezingTorrent>();
        var plainHits = plain.Damage("Freezing Torrent");
        var f = AfterBurstingIce(wait);

        f.Cast<FreezingTorrent>();
        f.Wait(4.0);

        var hits = f.Damage("Freezing Torrent");
        var end = EmbraceEnd(f);
        Assert.Equal(plainHits.Count, hits.Count);
        for (int i = 0; i < hits.Count; i++) AssertBuffed(hits[i].Value / plainHits[i].Value, hits[i].Time < end);
        if (wait == 2.0) Assert.Contains(hits, h => h.Time > end); // the channel does go past the window
    }
}

/// <summary>
/// How much haste it takes to fit casts into the Winter's Embrace window. The window is 3 seconds from the end of the Bursting
/// Ice cast. Every cast of a rotation that starts inside it is buffed.
/// Dev: a cast after Bursting Ice starts k global cooldowns later (1.5 / (1 + haste) each), so the k-th instant cast is
/// buffed when (k - 1) x 1.5 / (1 + haste) is at most 3 seconds (the engine's window is 3.017s, so a cast at exactly 3.0s
/// is in). That gives: the 3rd cast needs 0% haste, the 4th 50%, the 5th 100%. Hastes are chosen at and around those.
/// A cast is buffed when its damage is 1.2 times that of the same ability cast alone (no crits).
/// </summary>
public class WintersEmbraceHasteBreakpoints
{
    private const double Window = 3.0;
    private static readonly double[] Hastes = [0.0, 0.25, 0.45, 0.50, 0.55, 0.95, 1.00, 1.05];

    public static IEnumerable<object[]> HasteValues() => Hastes.Select(h => new object[] { h });

    private static double Alone(string ability, Action<StepDriver> cast, double haste)
    {
        var f = StepFights.Rime(haste);
        f.RimeOf().WinterOrbs = 2;
        cast(f);
        f.Wait(4.0);
        return f.Damage(ability).First().Value;
    }

    /// <summary>
    /// Player: after Bursting Ice, the rotation Ice Comet, Ice Comet, Cold Snap, Cold Snap, Ice Comet (all instant, on the
    /// global cooldown). Each cast is buffed if it starts within 3 seconds of the end of the Bursting Ice cast: with no haste
    /// the first three are, with 50% haste the fourth is too, and all five need 100% haste.
    /// Dev: orbs set to 4 (the two Cold Snaps give the 2 the last comet needs). Each cast's damage is compared with the same
    /// ability cast alone at the same haste; buffed (1.2) when its start (k - 1 global cooldowns after the first cast) is at
    /// most 3.0s, not buffed (1.0) after.
    /// </summary>
    [Theory]
    [MemberData(nameof(HasteValues))]
    public void Instant_Rotation_After_Bursting_Ice_Is_Buffed_While_It_Starts_In_The_Window(double haste)
    {
        var f = StepFights.Rime(haste);
        f.RimeOf().WinterOrbs = 4;
        f.Cast<BurstingIce>();
        string[] rotation = ["Ice Comet", "Ice Comet", "Cold Snap", "Cold Snap", "Ice Comet"];
        foreach (var ability in rotation)
        {
            if (ability == "Ice Comet") f.Cast<IceComet>(); else f.Cast<ColdSnap>();
        }
        f.Wait(4.0);

        double comet = Alone("Ice Comet", p => p.Cast<IceComet>(), haste);
        double snap = Alone("Cold Snap", p => p.Cast<ColdSnap>(), haste);
        var comets = f.Damage("Ice Comet").OrderBy(h => h.Time).ToList();
        var snaps = f.Damage("Cold Snap").OrderBy(h => h.Time).ToList();
        int cometIndex = 0, snapIndex = 0;
        for (int k = 1; k <= rotation.Length; k++)
        {
            double ratio = rotation[k - 1] == "Ice Comet" ? comets[cometIndex++].Value / comet : snaps[snapIndex++].Value / snap;
            bool inWindow = (k - 1) * 1.5 / (1 + haste) <= Window + 1e-9;
            Assert.True(Math.Abs(ratio - (inWindow ? 1.2 : 1.0)) < 0.001, $"cast {k} ({rotation[k - 1]}) at {haste:P0} haste: ratio {ratio:F3}, expected {(inWindow ? 1.2 : 1.0)}");
        }
    }

    /// <summary>
    /// Player: after Bursting Ice, the rotation Freezing Torrent, Cold Snap, Cold Snap. The torrent's 2 second channel is not
    /// shortened by haste, so the first Cold Snap starts at 2.0s and is always buffed; the second starts one global cooldown
    /// later and is buffed from 50% haste.
    /// Dev: each Cold Snap's damage is compared with a Cold Snap alone at the same haste; the second is buffed when 2.0 + 1.5 /
    /// (1 + haste) is at most 3.0s. The torrent's ticks all happen in the window and are each buffed (compared hit by hit with
    /// a torrent alone).
    /// </summary>
    [Theory]
    [MemberData(nameof(HasteValues))]
    public void Torrent_Rotation_After_Bursting_Ice_Is_Buffed_While_It_Starts_In_The_Window(double haste)
    {
        var f = StepFights.Rime(haste);
        f.Cast<BurstingIce>();
        f.Cast<FreezingTorrent>();
        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();
        f.Wait(4.0);
        var plain = StepFights.Rime(haste);
        plain.Cast<FreezingTorrent>();
        double snap = Alone("Cold Snap", p => p.Cast<ColdSnap>(), haste);

        var snaps = f.Damage("Cold Snap").OrderBy(h => h.Time).ToList();
        Assert.True(Math.Abs(snaps[0].Value / snap - 1.2) < 0.001, "the first Cold Snap starts at 2.0s: buffed");
        bool secondInWindow = 2.0 + 1.5 / (1 + haste) <= Window + 1e-9;
        Assert.True(Math.Abs(snaps[1].Value / snap - (secondInWindow ? 1.2 : 1.0)) < 0.001, $"second Cold Snap at {haste:P0} haste");
        var hits = f.Damage("Freezing Torrent");
        var plainHits = plain.Damage("Freezing Torrent");
        Assert.Equal(plainHits.Count, hits.Count);
        for (int i = 0; i < hits.Count; i++) Assert.True(Math.Abs(hits[i].Value / plainHits[i].Value - 1.2) < 0.001, $"torrent hit {i + 1}");
    }
}
