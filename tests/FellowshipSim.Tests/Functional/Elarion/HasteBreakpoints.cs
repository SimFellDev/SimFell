using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits Elarion's periodic abilities deal, and how many casts fit in a window, at each haste. Haste speeds the ticks up,
/// so the numbers go up in steps (breakpoints) rather than smoothly.
/// Dev: counts the damage events of one cast on a single enemy and compares them with the formula. The formulas are computed in
/// floating point, so a value that is a whole number in theory is rounded with a tolerance before the floor.
/// </summary>
public class ElarionHasteBreakpoints
{
    private const double Epsilon = 1e-9;

    /// <summary>The engine counts a last, partial tick only when at least one server frame (1/30 second) is left after the last full one.</summary>
    private const double ServerFrame = 1.0 / 30.0;

    private static int Floor(double x) => (int)Math.Floor(x + Epsilon);

    // ---- Starfall Volley

    /// <summary>
    /// Player: Starfall Volley lasts 8 seconds and strikes once a second, faster with haste: once when you cast it, then
    /// floor(8 x (1 + haste)) more times. The numbers of strikes go up at 0%, 12.5%, 25%, ...
    /// Dev: casts one Starfall Volley on one enemy at 0%, 25%, 49.9% and 50% haste and counts its hits after 12 seconds; expects
    /// 1 + floor(8 x (1 + haste)): 9, 11, 12 and 13.
    /// TODO: the engine releases no arrow in the last 0.3s of the volley (the arrows would land after it, from combat logs), so at
    /// 0% haste it strikes 8 times, not 9. The count here is the design's (the Python sim's and the C++ test's). Check in game.
    /// </summary>
    [Theory]
    [InlineData(0.0, 9)]
    [InlineData(0.25, 11)]
    [InlineData(0.499, 12)]
    [InlineData(0.5, 13)]
    public void Starfall_Volley_Strikes_Once_Plus_Floor_Of_8_Times_1_Plus_Haste(double haste, int hits)
    {
        Assert.Equal(hits, 1 + Floor(8 * (1 + haste)));
        var f = StepFights.Elarion(haste);

        f.Cast<StarfallVolley>();
        f.Wait(12);

        Assert.Equal(hits, f.Damage("Starfall Volley").Count);
    }

    /// <summary>
    /// Player: a Starfall Volley keeps the haste you had when you cast it: if your haste drops during the volley, it still strikes as
    /// often as it would have.
    /// Dev: 20% haste and Skystrider's Grace (+30%, 20s) up, so 50%; Starfall Volley cast; then the Grace buff is removed during the
    /// volley; the volley strikes 13 times, as at 50% haste.
    /// TODO: see the TODO of the breakpoints above for the engine's count.
    /// </summary>
    [Fact]
    public void Starfall_Volley_Keeps_The_Haste_It_Was_Cast_With()
    {
        var f = StepFights.Elarion(0.2);
        f.Cast<SkystridersGrace>();

        f.Cast<StarfallVolley>();
        f.Hero.Auras.Remove(SkystridersGrace.Buff, f.Now);
        f.Wait(12);

        Assert.Equal(13, f.Damage("Starfall Volley").Count);
    }

    // ---- Heartseeker Barrage

    /// <summary>
    /// Player: Heartseeker Barrage channels for 2 seconds (2.5 with Fusillade) and fires an arrow every 0.2 seconds, faster with
    /// haste: one for every full tick that fits in the channel, floor(channel / 0.2 x (1 + haste)), plus a last partial one when
    /// the time left after the last full tick is at least one server frame (1/30 second).
    /// Dev: this base class checks the formula for each variant below, at haste steps of 10%. A variant gives its talents and the
    /// channel length. Counts the damage events of one cast on one enemy; the cast returns when the channel is over.
    /// TODO: the Python sim and the C++ test count no partial arrow (floor only), so at some hastes they have one fewer than this
    /// (for example 12 arrows with Fusillade at 0% haste, against 13 here). The engine's rule is fitted to combat logs (a 2.5s channel
    /// at 73% haste fires 22 arrows), as for Rime's Freezing Torrent. To discuss with the other contributors.
    /// </summary>
    public abstract class BarrageArrows
    {
        private const double TickPeriod = 0.2;

        protected abstract int[]? Talents { get; }

        /// <summary>The channel length, before haste (which does not shorten it).</summary>
        protected abstract double ChannelSeconds { get; }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.1)]
        [InlineData(0.2)]
        [InlineData(0.3)]
        [InlineData(0.4)]
        [InlineData(0.5)]
        [InlineData(0.6)]
        [InlineData(0.7)]
        [InlineData(0.8)]
        [InlineData(0.9)]
        public void Arrows_Are_Floor_Of_Channel_Over_Period_Times_1_Plus_Haste_Plus_A_Partial_Over_One_Server_Frame(double haste)
        {
            int fullTicks = Floor(ChannelSeconds / TickPeriod * (1 + haste));
            double timeLeft = ChannelSeconds - fullTicks * TickPeriod / (1 + haste);
            int expected = fullTicks + (timeLeft >= ServerFrame - Epsilon ? 1 : 0);
            var f = StepFights.Elarion(haste, talents: Talents);

            f.Cast<HeartseekerBarrage>();
            f.Wait(1);

            Assert.Equal(expected, f.Damage("Heartseeker Barrage").Count);
        }
    }

    public class BaseBarrageArrows : BarrageArrows
    {
        protected override int[]? Talents => null;
        protected override double ChannelSeconds => 2.0;
    }

    public class FusilladeBarrageArrows : BarrageArrows
    {
        protected override int[]? Talents => [ElarionTalents.Fusillade.TalentId];
        protected override double ChannelSeconds => 2.5;
    }

    // ---- Skystrider's Supremacy

    /// <summary>
    /// Player: Skystrider's Supremacy empowers the Multishots you start in the next 4 seconds, so how many fit depends on haste: 3 at
    /// no haste, 4 from 12.5%, 5 from 50%, 6 from 87.5%. Each fires 3 arrows (at one enemy) and the next Multishot is not empowered.
    /// Dev: Supremacy cast, then back-to-back Multishots at the haste; the expected number of them have 3 arrows each, and the one
    /// after has 1. Hastes are placed just either side of each breakpoint.
    /// TODO: these breakpoints are hard to reach in game (the first Multishot has to follow the Supremacy at once), and the engine
    /// rounds cast times to the millisecond (87.4% and 87.6% haste both give 0.8s), so the pair around 87.5% may disagree.
    /// </summary>
    [Theory]
    [InlineData(0.0, 3)]
    [InlineData(0.124, 3)]
    [InlineData(0.126, 4)]
    [InlineData(0.49, 4)]
    [InlineData(0.51, 5)]
    [InlineData(0.874, 5)]
    [InlineData(0.876, 6)]
    public void Skystriders_Supremacy_Empowers_3_To_6_Multishots_By_Haste(double haste, int casts)
    {
        var f = StepFights.Elarion(haste);
        f.Cast<SkystridersSupremacy>();

        for (int i = 0; i < casts; i++) f.Cast<Multishot>();
        Assert.Equal(3 * casts, f.Damage("Multishot").Count);

        f.Cast<Multishot>();
        Assert.Equal(3 * casts + 1, f.Damage("Multishot").Count);
    }
}
