using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using RimeTalents = FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's abilities working together, on a bare Rime (no talents unless the test is about one). One ability on its own
/// is in AbilitiesSimple.cs. Winter's Embrace (the Bursting Ice buff) has its own section of buff tests.
/// </summary>
public class AbilitiesComplex
{
    private static double FrostBoltDamage(Action<StepDriver> before)
    {
        var f = StepFights.Rime();
        before(f);
        f.Cast<FrostBolt>();
        return f.Damage("Frost Bolt").First().Value;
    }

    private static List<StepEvent> SwallowHits(StepDriver f) => f.Damage("Frost Swallows");

    // ---- Flight of the Navir

    /// <summary>
    /// Player: with Flight of the Navir up, a Cold Snap sends all 5 circling swallows at its target, on top of the 3
    /// swallows that the Winter Orb it gives sends.
    /// Dev: Flight of the Navir, then a Cold Snap, then a 3s wait; counts the Frost Swallows damage events: the circling
    /// swallows and the orb's are logged under the same name, so the total is 5 + 3.
    /// </summary>
    [Fact]
    public void Cold_Snap_With_Flight_Of_The_Navir_Sends_5_Circling_Swallows_On_Top_Of_The_Orbs_3()
    {
        var f = StepFights.Rime();

        f.Cast<FlightOfTheNavir>();
        f.Cast<ColdSnap>();
        f.Wait(3.0);

        Assert.Equal(5 + 3, SwallowHits(f).Count);
    }

    /// <summary>
    /// Player: a circling swallow that is still out attacking is not sent again: a Cold Snap cast while a swallow from
    /// Freezing Torrent is still flying sends only 4 circling swallows (plus the orb's 3).
    /// Dev: SKELETON, not written. The scenario is: Flight of the Navir up; a Freezing Torrent tick sends one swallow; a
    /// Cold Snap is cast before that swallow has hit; expect 1 + 4 + 3 swallow hits. It needs a swallow travel time
    /// (today a swallow hits the instant it is sent) and a way to cast during a channel (the step driver waits until the
    /// hero is free). Skipped until it is confirmed in game.
    /// </summary>
    [Fact(Skip = "Skeleton: to confirm in game; needs a swallow travel time and casting during a channel")]
    public void Cold_Snap_Does_Not_Resend_A_Swallow_Still_In_Flight()
    {
        Assert.Fail("Not written yet: see the description of this test");
    }

    /// <summary>
    /// Player: with Flight of the Navir up, every tick of Freezing Torrent, the partial one included, sends one circling
    /// swallow at an enemy. This holds for every variant of the torrent.
    /// Dev: the variants below give the talents of the torrent; Flight of the Navir is cast first. Counts the Frost
    /// Swallows damage events: one per Freezing Torrent hit, plus 3 for each Winter Orb the Anima of a long channel makes.
    /// May fail at high haste once swallows have a travel time (a swallow that is out cannot be sent again).
    /// </summary>
    public abstract class TorrentWithFlight
    {
        protected abstract int[]? Talents { get; }

        protected virtual void Prepare(StepDriver fight) { }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.25)]
        [InlineData(0.50)]
        [InlineData(1.00)]
        public void Every_Torrent_Tick_Sends_A_Circling_Swallow(double haste)
        {
            var f = StepFights.Rime(haste, talents: Talents);
            f.Cast<FlightOfTheNavir>();
            Prepare(f);

            f.Cast<FreezingTorrent>();
            f.Wait(2.0);

            int ticks = f.Damage("Freezing Torrent").Count;
            int orbs = f.Events.Count(e => e.Kind == StepEventKind.ResourceGained && e.Name == "Winter Orbs");
            Assert.True(ticks > 0);
            Assert.Equal(ticks + Rime.SwallowsPerWinterOrb * orbs, SwallowHits(f).Count);
        }
    }

    /// <summary>Player: the plain Freezing Torrent. Dev: no talents.</summary>
    public class BaseTorrentWithFlight : TorrentWithFlight
    {
        protected override int[]? Talents => null;
    }

    /// <summary>Player: with Supreme Torrent. Dev: the Supreme Torrent talent.</summary>
    public class SupremeTorrentWithFlight : TorrentWithFlight
    {
        protected override int[]? Talents => [RimeTalents.SupremeTorrent.TalentId];
    }

    /// <summary>Player: with Soulfrost Torrent active. Dev: the talent, with its buff applied after Flight of the Navir.</summary>
    public class SoulfrostTorrentWithFlight : TorrentWithFlight
    {
        protected override int[]? Talents => [RimeTalents.SoulfrostTorrent.TalentId];

        protected override void Prepare(StepDriver fight) =>
            fight.Hero.Auras.Apply(RimeTalents.SoulfrostTorrent.Buff, fight.Now);
    }

    /// <summary>Player: with Supreme Torrent and Soulfrost Torrent together. Dev: both talents, Soulfrost's buff applied.</summary>
    public class SupremeSoulfrostTorrentWithFlight : TorrentWithFlight
    {
        protected override int[]? Talents => [RimeTalents.SupremeTorrent.TalentId, RimeTalents.SoulfrostTorrent.TalentId];

        protected override void Prepare(StepDriver fight) =>
            fight.Hero.Auras.Apply(RimeTalents.SoulfrostTorrent.Buff, fight.Now);
    }

    // ---- Ice Blitz

    /// <summary>
    /// Player: every Frost Swallow that hits adds 0.1 seconds to Ice Blitz. A Cold Snap during Ice Blitz sends 3 swallows
    /// (its orb's), so Ice Blitz lasts 20.3 seconds; with Flight of the Navir up it sends 8, so it lasts 20.8.
    /// Dev: Ice Blitz at the start of the fight (it does not use the global cooldown), Flight of the Navir when asked, a
    /// Cold Snap, then the buff is checked 0.1s before and 0.1s after its extended end, counted from the Ice Blitz cast.
    /// </summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 8)]
    public void Each_Swallow_Hit_Extends_Ice_Blitz_By_0_1_Seconds(bool withFlight, int swallowHits)
    {
        var f = StepFights.Rime();
        f.Cast<IceBlitz>();
        if (withFlight) f.Cast<FlightOfTheNavir>();

        f.Cast<ColdSnap>();
        f.Wait(3.0);

        Assert.Equal(swallowHits, SwallowHits(f).Count);
        double end = 20.0 + 0.1 * swallowHits;
        f.Wait(end - 0.1 - f.Now.TotalSeconds);
        Assert.True(f.Hero.Auras.HasAura(IceBlitz.Buff, f.Now));
        f.Wait(0.2);
        Assert.False(f.Hero.Auras.HasAura(IceBlitz.Buff, f.Now));
    }

    /// <summary>
    /// Player: Ice Blitz (+20% damage) and Wrath of Winter (+20% damage) stack by multiplying: with both up you deal
    /// 1.2 x 1.2 = 1.44 times the damage.
    /// Dev: compares the first Frost Bolt cast after Ice Blitz and Wrath of Winter (100 Spirit set) with one cast without
    /// them (no crits, so exact); expects 1.44, within 0.001.
    /// </summary>
    [Fact]
    public void Ice_Blitz_And_Wrath_Of_Winter_Damage_Bonuses_Multiply()
    {
        double plain = FrostBoltDamage(_ => { });
        double both = FrostBoltDamage(f =>
        {
            f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
            f.Cast<IceBlitz>();
            f.Cast<WrathOfWinter>();
        });

        Assert.InRange(both / plain, 1.44 - 0.001, 1.44 + 0.001);
    }

    // ---- Spirit Refund

    /// <summary>
    /// Player: with 20% Spirit, spending Winter Orbs has a chance (Spirit / (1 + Spirit)) to refund them. When it happens
    /// you get the orbs back a moment later, you gain a Rising Spirit stack, and you gain 2 Spirit Points.
    /// Dev: a Rime with 20% Spirit; the refund chance forced on (and a control fight with it forced off); orbs set to 3,
    /// one Glacial Blast (2 orbs). Right after the cast 1 orb is left; 0.3s later the 2 orbs are back (they come 0.2s
    /// after the spend), so 3. The Rising Spirit buff has 1 stack (none in the control). Spirit Points are 2 higher than
    /// in the control fight (damage also gives some, so the control is the reference).
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_The_Orbs_A_Rising_Spirit_Stack_And_2_Spirit_Points()
    {
        StepDriver Fight(bool refund)
        {
            var f = StepFights.Rime(spirit: 0.2);
            f.ForceProcs(key => key == Hero.SpiritRefundProc ? refund : null);
            f.RimeOf().WinterOrbs = 3;
            f.Cast<GlacialBlast>();
            return f;
        }
        var refunded = Fight(true);
        var control = Fight(false);
        Assert.Equal(0.2, refunded.Hero.SpiritPercent(refunded.Now), 9);

        Assert.Equal(1, refunded.RimeOf().WinterOrbs); // the orbs have not come back yet
        refunded.Wait(0.3);
        control.Wait(0.3);

        Assert.Equal(3, refunded.RimeOf().WinterOrbs);
        Assert.Equal(1, control.RimeOf().WinterOrbs);
        Assert.Equal(1, refunded.Hero.Auras.GetStacks(Hero.RisingSpirit, refunded.Now));
        Assert.Equal(0, control.Hero.Auras.GetStacks(Hero.RisingSpirit, control.Now));
        Assert.Equal(2.0, refunded.Hero.ResourceAmount(Hero.SpiritPoints) - control.Hero.ResourceAmount(Hero.SpiritPoints), 9);
    }

    // ---- Wrath of Winter

    /// <summary>
    /// Wrath of Winter's +30% haste (Spirit of Heroism) changes the other abilities while it lasts.
    /// Dev: each test casts Wrath of Winter (100 Spirit set) first, then the ability, and measures its cast from the
    /// cast-started to the cast-completed event, so the global cooldown does not hide it.
    /// </summary>
    public class WrathOfWinterWithOtherAbilities
    {
        private static StepDriver WrathUp(double baseHaste)
        {
            var f = StepFights.Rime(baseHaste);
            f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
            f.Cast<WrathOfWinter>();
            return f;
        }

        private static double CastSeconds(StepDriver f, string ability)
        {
            var started = f.Events.Last(e => e.Kind == StepEventKind.CastStarted && e.Name == ability);
            var completed = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted && e.Name == ability);
            return (completed.Time - started.Time).TotalSeconds;
        }

        /// <summary>
        /// Player: while Wrath of Winter is up, Glacial Blast is instant, and it still costs 2 Winter Orbs.
        /// Dev: orbs set to 3 after Wrath; one Glacial Blast; the cast-started and cast-completed events are at the same
        /// time, and 1 orb is left.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.2)]
        [InlineData(0.4)]
        public void Glacial_Blast_Is_Instant_And_Costs_2_Orbs_During_Wrath_Of_Winter(double baseHaste)
        {
            var f = WrathUp(baseHaste);
            f.RimeOf().WinterOrbs = 3;

            f.Cast<GlacialBlast>();

            Assert.Equal(0.0, CastSeconds(f, "Glacial Blast"));
            Assert.Equal(1, f.RimeOf().WinterOrbs);
        }

        /// <summary>
        /// Player: while Wrath of Winter is up its +30% haste adds to yours: a Frost Bolt takes 1.5 seconds divided by
        /// (1 + your haste + 0.3).
        /// Dev: one Frost Bolt after Wrath at 0%, 20% and 40% base haste; expects 1.5 / (1 + base + 0.3), within 1ms.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.2)]
        [InlineData(0.4)]
        public void Frost_Bolt_Is_Hasted_By_Wrath_Of_Winter(double baseHaste)
        {
            var f = WrathUp(baseHaste);

            f.Cast<FrostBolt>();

            Assert.InRange(CastSeconds(f, "Frost Bolt"), 1.5 / (1 + baseHaste + 0.3) - 0.001, 1.5 / (1 + baseHaste + 0.3) + 0.001);
        }
    }
}
