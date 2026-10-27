using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using RimeTalents = FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's Anima and Winter Orb economy: what builds it, what spends it, and the talents that change either.
/// Each test has a "Player" line (what it checks, in game terms) and a "Dev" line (how it is done).
/// </summary>
public class ResourceEconomy
{
    private static double Anima(StepDriver f) => f.Hero.ResourceAmount(Rime.Anima);

    // ---- Builders and spenders

    /// <summary>
    /// Player: a Frost Bolt gives 3 Anima, and you get it when the cast finishes, not when you start it.
    /// Dev: casts one bolt on a bare Rime (no talents, no haste); checks the Anima total and the time of the single
    /// Anima-gained event (1.5s, the end of the cast).
    /// </summary>
    [Fact]
    public void Frost_Bolt_Grants_3_Anima_When_The_Cast_Completes()
    {
        var f = StepFights.Rime();

        f.Cast<FrostBolt>();

        Assert.Equal(3, Anima(f));
        var gained = Assert.Single(f.Events, e => e.Kind == StepEventKind.ResourceGained && e.Name == "Anima");
        Assert.Equal(3, gained.Value);
        Assert.Equal(SimTime.FromSeconds(1.5), gained.Time); // not at the cast start (0s)
    }

    /// <summary>
    /// Player: every 9 Anima turn into a Winter Orb and your Anima starts again from 0.
    /// Dev: three Frost Bolts (3 + 3 + 3); no orb after two, one orb and 0 Anima after the third.
    /// </summary>
    [Fact]
    public void Nine_Anima_Make_A_Winter_Orb_And_Reset_Anima()
    {
        var f = StepFights.Rime();

        f.Cast<FrostBolt>();
        f.Cast<FrostBolt>();
        Assert.Equal(0, f.RimeOf().WinterOrbs);
        f.Cast<FrostBolt>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
        Assert.Equal(0, Anima(f));
    }

    /// <summary>
    /// Player: Anima past 9 is not lost: you get the orb and keep the extra.
    /// Dev: Frost Bolts alone only reach 0, 3, 6, 9, so the Anima is set to 8 directly, then one bolt makes 11:
    /// one orb and 2 Anima left.
    /// </summary>
    [Fact]
    public void Anima_Above_Nine_Keeps_The_Excess_After_Making_The_Orb()
    {
        var f = StepFights.Rime();
        f.Hero.Resources.Get(Rime.Anima).Current = 8;

        f.Cast<FrostBolt>(); // 8 + 3 = 11

        Assert.Equal(1, f.RimeOf().WinterOrbs);
        Assert.Equal(2, Anima(f));
    }

    /// <summary>
    /// Player: you can hold at most 5 Winter Orbs; one more is lost.
    /// Dev: orbs are set to 5 directly, then a Cold Snap (+1 orb) is cast; checks the orbs stay at 5 and an
    /// overflow event for Winter Orbs was recorded.
    /// </summary>
    [Fact]
    public void Winter_Orbs_Are_Capped_At_5()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 5;

        f.Cast<ColdSnap>(); // +1 orb

        Assert.Equal(5, f.RimeOf().WinterOrbs);
        Assert.Contains(f.Events, e => e.Kind == StepEventKind.ResourceOverflow && e.Name == "Winter Orbs");
    }

    /// <summary>
    /// Player: Cold Snap gives you 1 Winter Orb.
    /// Dev: one Cold Snap from 0 orbs; checks the orb count is 1.
    /// </summary>
    [Fact]
    public void Cold_Snap_Grants_1_Winter_Orb()
    {
        var f = StepFights.Rime();

        f.Cast<ColdSnap>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>
    /// Player: Glacial Blast costs 2 Winter Orbs.
    /// Dev: orbs set to 3, one Glacial Blast (a 1.5s cast, so the spend happens at its completion); checks 1 orb is left.
    /// </summary>
    [Fact]
    public void Glacial_Blast_Costs_2_Winter_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 3;

        f.Cast<GlacialBlast>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>
    /// Player: Ice Comet costs 2 Winter Orbs.
    /// Dev: orbs set to 3, one Ice Comet; checks 1 orb is left.
    /// </summary>
    [Fact]
    public void Ice_Comet_Costs_2_Winter_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 3;

        f.Cast<IceComet>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>
    /// Player: with only 1 Winter Orb you cannot cast Glacial Blast or Ice Comet, and the orb is not used up.
    /// Dev: orbs set to 1; TryCast on both returns a refusal (no time passes); the orb count stays 1.
    /// </summary>
    [Fact]
    public void Spenders_Are_Refused_With_Fewer_Than_2_Winter_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 1;

        Assert.False(f.TryCast<GlacialBlast>().Succeeded);
        Assert.False(f.TryCast<IceComet>().Succeeded);
        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    /// <summary>
    /// Player: Glacial Blast and Ice Comet use the same Winter Orbs: with 5 orbs you can cast one of each (2 + 2), and
    /// then neither is available with the 1 orb left. A Glacial Blast still costs 2 orbs while Wrath of Winter is up
    /// (see AbilitiesComplex).
    /// Dev: orbs set to 5; a Glacial Blast, then an Ice Comet; checks 1 orb is left and both spenders are refused.
    /// </summary>
    [Fact]
    public void Glacial_Blast_And_Ice_Comet_Spend_From_The_Same_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 5;

        f.Cast<GlacialBlast>();
        f.Cast<IceComet>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
        Assert.False(f.TryCast<GlacialBlast>().Succeeded);
        Assert.False(f.TryCast<IceComet>().Succeeded);
    }

    // ---- Anima per pulse / tick, orbs over time

    /// <summary>
    /// Player: Bursting Ice gives 1 Anima for each of its pulses, however many enemies the pulse hits and at any haste.
    /// Dev: casts Bursting Ice at 0% and 50% haste with 1 and 3 enemies, then waits 4s so every pulse has fired (the
    /// debuff lasts 3s). A pulse is a distinct timestamp among the Bursting Ice damage events, so a pulse that hits
    /// 3 enemies counts once. Checks one Anima gain per pulse, each worth 1.
    /// </summary>
    [Theory]
    [InlineData(0.0, 1)]
    [InlineData(0.0, 3)]
    [InlineData(0.5, 1)]
    [InlineData(0.5, 3)]
    public void Bursting_Ice_Grants_1_Anima_Per_Pulse_Whatever_The_Number_Of_Targets(double haste, int enemies)
    {
        var f = StepFights.Rime(haste, enemies);

        f.Cast<BurstingIce>();
        f.Wait(4.0); // every pulse (the debuff lasts 3s)

        var pulses = f.Damage("Bursting Ice").Select(e => e.Time).Distinct().Count();
        var gains = f.AnimaGains();
        Assert.True(pulses > 0);
        Assert.Equal(pulses, gains.Count);
        Assert.All(gains, g => Assert.Equal(1, g.Value));
    }

    /// <summary>
    /// Player: Freezing Torrent gives 1 Anima for each tick of the channel, at any haste.
    /// Dev: casts it at 0% and 50% haste (the cast returns when the channel is over) and waits 3s more. A tick is a
    /// Freezing Torrent damage event (one enemy). Checks one Anima gain per tick, each worth 1.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Freezing_Torrent_Grants_1_Anima_Per_Tick(double haste)
    {
        var f = StepFights.Rime(haste);

        f.Cast<FreezingTorrent>();
        f.Wait(3.0);

        var ticks = f.Damage("Freezing Torrent").Count;
        var gains = f.AnimaGains();
        Assert.True(ticks > 0);
        Assert.Equal(ticks, gains.Count);
        Assert.All(gains, g => Assert.Equal(1, g.Value));
    }

    private const double SpiritOfHeroismHaste = 0.30; // Wrath of Winter's buff, active when its orbs are timed

    /// <summary>
    /// Player: Wrath of Winter gives you a Winter Orb every 4 seconds, shortened by your haste. Casting it also gives
    /// you +30% haste, which counts.
    /// Dev: 100 Spirit is set so the cast is allowed; the cast is followed by a 20s wait (the buff's length). Takes
    /// the first 5 orb gains (the cap, so later ones would not be recorded): the activation orb and 4 more. The gap
    /// between orbs must be 4 / (1 + base haste + 0.30), within 2ms because the engine rounds times to the millisecond.
    /// The +30% is added to base haste, as the log note in Rime's NOTES.md suggests (about 2.4s at about 69% haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Wrath_Of_Winter_Grants_1_Winter_Orb_Every_4_Seconds_Over_1_Plus_Haste(double baseHaste)
    {
        var f = StepFights.Rime(baseHaste);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

        f.Cast<WrathOfWinter>();
        f.Wait(20.0);

        // 5 orbs fill the cap: the activation orb and 4 more.
        var orbs = f.Events.Where(e => e.Kind == StepEventKind.ResourceGained && e.Name == "Winter Orbs").Take(5).ToList();
        Assert.Equal(5, orbs.Count);
        double expected = 4.0 / (1.0 + baseHaste + SpiritOfHeroismHaste);
        for (int i = 1; i < orbs.Count; i++)
        {
            double gap = (orbs[i].Time - orbs[i - 1].Time).TotalSeconds;
            Assert.InRange(gap, expected - 0.002, expected + 0.002); // times are whole milliseconds
        }
    }

    // ---- Icy Talons spenders

    /// <summary>
    /// Player: with Icy Talons, Talon Strike uses up all your Winter Orbs and strikes once for each.
    /// Dev: the Icy Talons talent, orbs set to 1 to 5, one Talon Strike, then a 3s wait for the spikes to land. Checks
    /// 0 orbs left, one spend event of exactly that many orbs, and as many Talon Strike damage events.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Talon_Strike_Spends_All_Winter_Orbs(int orbs)
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);
        f.RimeOf().WinterOrbs = orbs;

        f.Cast<TalonStrike>();
        f.Wait(3.0);

        Assert.Equal(0, f.RimeOf().WinterOrbs);
        var spent = Assert.Single(f.Events, e => e.Kind == StepEventKind.ResourceSpent && e.Name == "Winter Orbs");
        Assert.Equal(orbs, spent.Value);
        Assert.Equal(orbs, f.Damage("Talon Strike").Count); // one spike per orb
    }

    /// <summary>
    /// Player: you cannot use Talon Strike with no Winter Orbs.
    /// Dev: the Icy Talons talent, 0 orbs; TryCast returns a refusal.
    /// </summary>
    [Fact]
    public void Talon_Strike_Is_Refused_With_0_Winter_Orbs()
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);

        Assert.False(f.TryCast<TalonStrike>().Succeeded);
    }

    /// <summary>
    /// Player: with Icy Talons, Rising Talons uses up all your Winter Orbs and sends one wave for each.
    /// Dev: as the Talon Strike test, but with a 5s wait because the comets of the waves fall after the cast, and
    /// counting Rising Talons damage events.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Rising_Talons_Spends_All_Winter_Orbs(int orbs)
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);
        f.RimeOf().WinterOrbs = orbs;

        f.Cast<RisingTalons>();
        f.Wait(5.0); // the comets fall after the cast

        Assert.Equal(0, f.RimeOf().WinterOrbs);
        var spent = Assert.Single(f.Events, e => e.Kind == StepEventKind.ResourceSpent && e.Name == "Winter Orbs");
        Assert.Equal(orbs, spent.Value);
        Assert.Equal(orbs, f.Damage("Rising Talons").Count); // one wave per orb
    }

    /// <summary>
    /// Player: you cannot use Rising Talons with no Winter Orbs.
    /// Dev: the Icy Talons talent, 0 orbs; TryCast returns a refusal.
    /// </summary>
    [Fact]
    public void Rising_Talons_Is_Refused_With_0_Winter_Orbs()
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);

        Assert.False(f.TryCast<RisingTalons>().Succeeded);
    }

    // ---- Talents

    /// <summary>4 Glacial Assault stacks from 4 Cold Snaps (2 charges, so it waits for the recharge), then no Winter Orbs.</summary>
    private static void StackGlacialAssaultAndEmptyOrbs(StepDriver f)
    {
        for (int casts = 0; casts < GlacialAssault.MaxStacks;)
        {
            if (f.TryCast<ColdSnap>().Succeeded) casts++;
            else f.Wait(0.5);
        }
        Assert.Equal(GlacialAssault.MaxStacks, f.Hero.Auras.GetStacks(GlacialAssault.Buff, f.Now));
        f.RimeOf().WinterOrbs = 0;
    }

    /// <summary>
    /// Player: with Glacial Assault, once you have 4 stacks (one per Cold Snap) you can cast Glacial Blast for free,
    /// even with no Winter Orbs.
    /// Dev: the Glacial Assault talent. Four real Cold Snaps, waiting (0.5s steps) whenever the charges are used up,
    /// about 25s of fight time; then the orbs the snaps gave are set back to 0. Checks the cast is accepted and
    /// Glacial Blast damage appears after a 3s wait.
    /// </summary>
    [Fact]
    public void Glacial_Assault_Makes_Glacial_Blast_Castable_With_No_Winter_Orbs_At_4_Stacks()
    {
        var f = StepFights.Rime(talents: [GlacialAssault.TalentId]);
        StackGlacialAssaultAndEmptyOrbs(f);

        var attempt = f.TryCast<GlacialBlast>();
        f.Wait(3.0);

        Assert.True(attempt.Succeeded, attempt.Reason);
        Assert.NotEmpty(f.Damage("Glacial Blast"));
    }

    /// <summary>
    /// Player: with Glacial Assault and Icy Talons, 4 stacks let you cast Talon Strike with no Winter Orbs, and it
    /// strikes 5 times, as if you had a full 5 orbs.
    /// Dev: both talents; the same 4-stack setup as the Glacial Blast test. Checks the cast is accepted and, after a
    /// 3s wait, there are exactly 5 Talon Strike damage events.
    /// </summary>
    [Fact]
    public void Glacial_Assault_Makes_Talon_Strike_Castable_With_No_Winter_Orbs_At_4_Stacks_And_Fires_5_Spikes()
    {
        var f = StepFights.Rime(talents: [GlacialAssault.TalentId, IcyTalons.TalentId]);
        StackGlacialAssaultAndEmptyOrbs(f);

        var attempt = f.TryCast<TalonStrike>();
        f.Wait(3.0);

        Assert.True(attempt.Succeeded, attempt.Reason);
        Assert.Equal(5, f.Damage("Talon Strike").Count);
    }

    /// <summary>
    /// Player: with Burstbolter, a Frost Bolt that hits gives 1 extra Anima, whatever the number of enemies around.
    /// The extra Anima comes when the bolt hits, not when you finish casting.
    /// Dev: the Burstbolter talent with 1, 3 and 5 enemies. After the cast a 1s wait leaves room for a travel time
    /// (today the engine lands the bolt the instant the cast completes, so the wait is not needed). Checks the bolt's
    /// damage landed, then two Anima gains: 3 at the cast's completion and 1 at the time of the bolt's damage.
    /// If a travel time longer than the wait is added, the "landed" assert fails and the wait must be lengthened.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Burstbolter_Grants_1_Anima_When_The_Frost_Bolt_Damage_Lands(int enemies)
    {
        var f = StepFights.Rime(enemies: enemies, talents: [Burstbolter.TalentId]);

        f.Cast<FrostBolt>();
        // The engine lands the bolt the instant the cast completes, so this wait is not needed today. It leaves room for
        // a travel time. If one longer than this wait is added, the "damage landed" assert fails: lengthen the wait.
        f.Wait(1.0);

        var landed = f.Damage("Frost Bolt");
        Assert.NotEmpty(landed); // the Frost Bolt damage landed
        var hit = landed.First();
        var completed = f.Events.First(e => e.Kind == StepEventKind.CastCompleted && e.Name == "Frost Bolt");
        var gains = f.AnimaGains();
        Assert.Equal(2, gains.Count);
        Assert.Equal(3, gains[0].Value); // the cast
        Assert.Equal(completed.Time, gains[0].Time);
        Assert.Equal(1, gains[1].Value); // Burstbolter's pulse, with the damage
        Assert.Equal(hit.Time, gains[1].Time);
    }

    /// <summary>
    /// Player: with Bursting Swallows, when it triggers, each Frost Swallow that hits gives 1 Anima. A Winter Orb
    /// sends 3 swallows, so a Cold Snap gives 3 Anima.
    /// Dev: the Bursting Swallows talent with its random trigger forced on for every swallow (ForcedProcs replaces the
    /// fight's outcome source). One Cold Snap, then a 3s wait for the swallows. Checks 3 swallow hits and as many
    /// Anima gains.
    /// </summary>
    [Fact]
    public void Bursting_Swallows_Grants_1_Anima_For_Each_Swallow_Hit_When_It_Procs()
    {
        var f = StepFights.Rime(talents: [BurstingSwallows.TalentId]);
        f.Context.Outcomes = new ForcedProcs(f.Context.Outcomes, key => key == BurstingSwallows.ProcKey ? true : null);

        f.Cast<ColdSnap>(); // +1 Winter Orb, which sends 3 Frost Swallows
        f.Wait(3.0);

        var swallowHits = f.Damage("Frost Swallows").Count;
        Assert.Equal(Rime.SwallowsPerWinterOrb, swallowHits);
        Assert.Equal(swallowHits, f.AnimaGains().Count);
    }

    /// <summary>
    /// Player: with Bursting Swallows, when it does not trigger, the swallows give no Anima.
    /// Dev: as the test above but with the trigger forced off. Checks swallows still hit and there are no Anima gains.
    /// </summary>
    [Fact]
    public void Bursting_Swallows_Grants_No_Anima_When_It_Does_Not_Proc()
    {
        var f = StepFights.Rime(talents: [BurstingSwallows.TalentId]);
        f.Context.Outcomes = new ForcedProcs(f.Context.Outcomes, key => key == BurstingSwallows.ProcKey ? false : null);

        f.Cast<ColdSnap>();
        f.Wait(3.0);

        Assert.NotEmpty(f.Damage("Frost Swallows"));
        Assert.Empty(f.AnimaGains());
    }

    // ---- Freezing Torrent: Anima per damaging tick, for the base channel and each talent that changes it

    /// <summary>
    /// Player: every tick of Freezing Torrent that deals damage gives 1 Anima, however the channel is changed
    /// (talents, haste). The subclasses below run the same check for each variant.
    /// Dev: at 0% and 50% haste, casts the torrent (the cast returns when the channel is over) and waits 4s more.
    /// Checks one Anima gain, worth 1, for each Freezing Torrent damage event (one enemy). The variant sets its talents
    /// and, if needed, its starting state in <see cref="Prepare"/>.
    /// </summary>
    public abstract class TorrentAnima
    {
        protected abstract int[]? Talents { get; }

        /// <summary>Puts the hero in the state the variant needs before it channels.</summary>
        protected virtual void Prepare(StepDriver fight) { }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        public void Anima_Gained_Equals_The_Damaging_Ticks(double haste)
        {
            var f = StepFights.Rime(haste, talents: Talents);
            Prepare(f);

            f.Cast<FreezingTorrent>();
            f.Wait(4.0);

            var ticks = f.Damage("Freezing Torrent").Count;
            var gains = f.AnimaGains();
            Assert.True(ticks > 0);
            Assert.Equal(ticks, gains.Count);
            Assert.All(gains, g => Assert.Equal(1, g.Value));
        }
    }

    /// <summary>Player: the plain Freezing Torrent. Dev: no talents.</summary>
    public class BaseTorrent : TorrentAnima
    {
        protected override int[]? Talents => null;
    }

    /// <summary>Player: with Supreme Torrent (a longer channel, a full final tick). Dev: the Supreme Torrent talent.</summary>
    public class SupremeTorrent : TorrentAnima
    {
        protected override int[]? Talents => [RimeTalents.SupremeTorrent.TalentId];
    }

    /// <summary>
    /// Player: with Soulfrost Torrent active (a faster channel). Dev: the Soulfrost Torrent talent; its buff normally
    /// comes from a Real PPM roll, so it is applied directly before the cast and the test checks it is up.
    /// </summary>
    public class SoulfrostTorrent : TorrentAnima
    {
        protected override int[]? Talents => [RimeTalents.SoulfrostTorrent.TalentId];

        protected override void Prepare(StepDriver fight)
        {
            fight.Hero.Auras.Apply(RimeTalents.SoulfrostTorrent.Buff, fight.Now);
            Assert.True(fight.Hero.Auras.HasAura(RimeTalents.SoulfrostTorrent.Buff, fight.Now));
        }
    }
}
