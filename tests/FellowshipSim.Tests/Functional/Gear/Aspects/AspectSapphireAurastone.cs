using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Sapphire Aurastone (aspect 32): "Your Spirit Ability causes you to conjure a Sapphire Aurastone. It lasts as long as the Spirit of Heroism effect from
/// your Spirit Ability is active on you. While active, 3.4% / 6.3% / 9.1% / 12% of all damage you deal is accumulated into the Sapphire Aurastone and
/// released in a pulse of damage, evenly split between nearby enemies every 3 seconds."
/// Dev: over the six heroes. The hero hits once (not stored: before the stone), casts its Spirit ability (which gives Spirit of Heroism, 20 seconds)
/// and then keeps hitting with its unit hit until the last seconds of it. Haste is 13% (Spirit of Heroism's +30% comes on top). The first pulse comes 6 seconds after the stone appears (combat logs), then every 3 seconds. The
/// damage dealt is read from the hero's damage events between two pulses. A hit that lands at the very instant of a pulse (Mara's poison ticks do)
/// may be stored before or after the pulse; the order is not a rule, so a pulse is held between the share of the damage without and with those hits.
/// </summary>
public class AspectSapphireAurastone
{
    private const string PulseName = "Sapphire Aurastone Damage";
    private static readonly double[] Share = [0.034, 0.063, 0.091, 0.12];

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> HeroRanks => AspectFights.HeroRanks;

    private sealed record Run(StepDriver Fight, double Start, double End);

    private static Run Play(GearHero hero, int rank = 4, int enemies = 1, double crit = 0.0) => PlayAt(hero, rank, enemies, crit, 0.13);

    private static Run PlayAt(GearHero hero, int rank, int enemies, double crit, double haste)
    {
        var f = hero.Fight(AspectFights.With(AspectFights.SapphireAurastone, rank, crit: crit, haste: haste, enemies: enemies));
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        AspectFights.CastWhenReady(f, hero, hero.Spirit);
        double start = f.Events.First(e => e.Kind == StepEventKind.AuraGained && e.Name == "Spirit of Heroism").Time.TotalSeconds;
        while (f.Now.TotalSeconds < start + 19.0) AspectFights.CastWhenReady(f, hero, hero.Unit);
        f.Wait(Math.Max(0.0, start + 23.0 - f.Now.TotalSeconds));
        double end = f.Events.First(e => e.Kind == StepEventKind.AuraRemoved && e.Name == "Spirit of Heroism" && e.Time.TotalSeconds >= start).Time.TotalSeconds;
        return new Run(f, start, end);
    }

    private static List<StepEvent> Pulses(StepDriver f) => f.AspectHits(PulseName);

    /// <summary>The distinct times of the pulses.</summary>
    private static List<double> PulseTimes(StepDriver f) => Pulses(f).Select(p => p.Time.TotalSeconds).Distinct().ToList();

    /// <summary>The damage the hero dealt strictly between two instants, not counting the stone's own pulses.</summary>
    private static double Dealt(StepDriver f, double from, double to, bool includeEdges = false) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.Name != PulseName
                            && (includeEdges ? e.Time.TotalSeconds >= from - 1e-9 && e.Time.TotalSeconds <= to + 1e-9
                                             : e.Time.TotalSeconds > from + 1e-9 && e.Time.TotalSeconds < to - 1e-9)).Sum(e => e.Value);

    // ---- When

    /// <summary>
    /// Player: the stone pulses for the first time 6 seconds after it appears, then every 3 seconds.
    /// Dev: the pulse times against start + 6, 9, 12, 15, 18.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_First_Pulse_Is_Six_Seconds_After_The_Spirit_Ability_Then_Every_Three(GearHero hero)
    {
        var run = Play(hero);
        var times = PulseTimes(run.Fight);
        Assert.Equal(5, times.Count);
        for (int i = 0; i < 5; i++) Assert.Equal(run.Start + 6.0 + 3.0 * i, times[i], 0.002);
    }

    /// <summary>
    /// Player: the stone lasts as long as Spirit of Heroism (20 seconds) and no longer.
    /// Dev: no pulse comes after Spirit of Heroism's removal (the 21st second would be the next).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Stone_Ends_With_Spirit_Of_Heroism(GearHero hero)
    {
        var run = Play(hero);
        Assert.Equal(run.Start + 20.0, run.End, 0.05); // the engine removes Spirit of Heroism about 17ms late (a server frame)
        Assert.All(PulseTimes(run.Fight), t => Assert.True(t <= run.End + 0.001));
    }

    /// <summary>
    /// Player: without the Spirit ability there is no stone.
    /// Dev: 25 seconds of hits with the unit hit; no pulse.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Spirit_Ability_No_Stone(GearHero hero)
    {
        var f = hero.Fight(AspectFights.With(AspectFights.SapphireAurastone, 4, haste: 0.13));
        while (f.Now.TotalSeconds < 25.0) AspectFights.CastWhenReady(f, hero, hero.Unit);
        Assert.Empty(Pulses(f));
    }

    // ---- How much

    /// <summary>
    /// Player: each pulse deals the rank's share (3.4% / 6.3% / 9.1% / 12%) of the damage you dealt since the last pulse (the first: since the stone
    /// appeared; what you did before it does not count).
    /// Dev: every pulse's total (all its hits) against share x the damage between the pulses. The first window starts at the stone's appearance: what
    /// the Spirit ability itself deals at that very instant, and hits at a pulse's instant, may or may not count (open question, see the notes), so a pulse
    /// is held between the share without and with them.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanks))]
    public void A_Pulse_Is_The_Rank_Share_Of_The_Damage_Since_The_Last(GearHero hero, int rank)
    {
        var run = Play(hero, rank);
        var times = PulseTimes(run.Fight);
        double previous = run.Start;
        foreach (double t in times)
        {
            double pulse = Pulses(run.Fight).Where(p => Math.Abs(p.Time.TotalSeconds - t) < 1e-9).Sum(p => p.Value);
            double without = Dealt(run.Fight, previous, t);
            double with = Dealt(run.Fight, previous, t, includeEdges: true);
            Assert.InRange(pulse, Share[rank - 1] * without - 1e-6, Share[rank - 1] * with + 1e-6);
            previous = t;
        }
    }

    /// <summary>
    /// Player: the pulse is split evenly between the nearby enemies.
    /// Dev: three enemies: every pulse has three equal hits, one per enemy, and their total is the share of the damage dealt.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Pulse_Is_Split_Evenly_Between_The_Enemies(GearHero hero)
    {
        var run = Play(hero, 4, enemies: 3);
        var pulses = Pulses(run.Fight);
        foreach (var group in pulses.GroupBy(p => p.Time))
        {
            Assert.Equal(3, group.Count());
            Assert.Equal(3, group.Select(p => p.Target).Distinct().Count());
            Assert.All(group, p => Assert.Equal(group.First().Value, p.Value, 1e-6));
        }
    }

    /// <summary>
    /// Player: the pulse never crits and does not scale with anything again.
    /// Dev: 100% crit chance; every pulse event has a crit chance of 0, and the pulse is still exactly the share of the damage dealt.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Pulse_Never_Crits(GearHero hero)
    {
        var run = Play(hero, 4, crit: 1.0);
        var pulses = Pulses(run.Fight);
        Assert.NotEmpty(pulses);
        Assert.All(pulses, p => Assert.Equal(0.0, p.CritChance));
        var times = PulseTimes(run.Fight);
        double second = pulses.Where(p => Math.Abs(p.Time.TotalSeconds - times[1]) < 1e-9).Sum(p => p.Value);
        Assert.InRange(second, 0.12 * Dealt(run.Fight, times[0], times[1]) - 1e-6, 0.12 * Dealt(run.Fight, times[0], times[1], includeEdges: true) + 1e-6);
    }
}
