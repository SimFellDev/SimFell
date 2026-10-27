using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Amethyst Splinters (aspect 31): "When you deal Critical Damage to an enemy, you deal an additional X% of that damage to them over 8 seconds",
/// X = 4% / 6% / 8% / 11% by rank. One splinter per enemy; a splinter never crits.
/// Dev: every test runs over the six heroes (and the four ranks where the rank matters). The hero has 100% crit chance so that its hit is a crit in
/// expected-value mode; Tariq's Wild Swing cannot crit, so his Face Breaker is the hit. A splinter's damage is compared with the damage of the
/// crit hits that made it ("S": all crit hits of the same instant, since each crit adds its share to the same splinter). No haste unless said.
/// </summary>
public class AspectAmethystSplinters
{
    private const int SplinterEffect = 101;
    private static readonly double[] Share = [0.04, 0.06, 0.08, 0.11];
    private const double Tolerance = 1e-6;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> HeroRanks => AspectFights.HeroRanks;

    private static StepDriver Crit(GearHero hero, int rank = 1, double haste = 0, double expertise = 0, int enemies = 1) =>
        hero.Fight(AspectFights.With(AspectFights.AmethystSplinters, rank, crit: 1.0, haste: haste, expertise: expertise, enemies: enemies));

    private static List<StepEvent> Splinters(StepDriver f) => f.AspectEffectHits(SplinterEffect);

    /// <summary>The crit hits (not splinter ticks) in order.</summary>
    private static List<StepEvent> CritHits(StepDriver f) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.CritChance > 0.5 && e.AbilityId != LogIds.Effect(SplinterEffect)).ToList();

    /// <summary>S: the damage of every crit hit that came at the instant of the first one.</summary>
    private static double FirstCritDamage(StepDriver f, out SimTime at)
    {
        var hits = CritHits(f);
        at = hits[0].Time;
        var instant = at;
        return hits.Where(h => h.Time == instant).Sum(h => h.Value);
    }

    // ---- Damage

    /// <summary>
    /// Player: a crit makes the splinter tick every 2 seconds for a quarter of the rank's share of the crit's damage (4% / 6% / 8% / 11%).
    /// Dev: one cast, then 10 seconds; the first splinter tick against share x S / 4.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanks))]
    public void A_Crit_Makes_A_Splinter_That_Ticks_For_A_Quarter_Of_The_Rank_Share(GearHero hero, int rank)
    {
        var f = Crit(hero, rank);
        hero.CastProbe(f, AspectFights.CritUnit(hero));
        f.Wait(10);
        double s = FirstCritDamage(f, out _);
        var ticks = Splinters(f);
        Assert.NotEmpty(ticks);
        Assert.Equal(Share[rank - 1] * s / 4.0, ticks[0].Value, 1e-6 * s);
    }

    /// <summary>
    /// Player: the splinter deals X% of the crit over 8 seconds: 4 ticks, 2 seconds apart, the last one as the 8 seconds end.
    /// Dev: no haste; tick times from the crit's instant; the total against X% x S. The engine used to end the splinter before a tick landing exactly 8.0s
    /// after the crit (3 ticks, 75% of the share); the last tick now lands.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Splinter_Deals_The_Whole_Share_In_Four_Ticks_Two_Seconds_Apart(GearHero hero)
    {
        var f = Crit(hero, 4);
        hero.CastProbe(f, AspectFights.CritUnit(hero));
        f.Wait(12);
        double s = FirstCritDamage(f, out var at);
        var ticks = Splinters(f);
        Assert.Equal(4, ticks.Count);
        for (int i = 0; i < ticks.Count; i++) Assert.Equal(at.TotalSeconds + 2.0 * (i + 1), ticks[i].Time.TotalSeconds, 0.002);
        Assert.Equal(0.11 * s, ticks.Sum(t => t.Value), 1e-6 * s);
    }

    /// <summary>
    /// Player: only critical strikes make a splinter.
    /// Dev: no crit chance; the hit's damage events carry no crit and nothing is dealt in 10 seconds.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Hit_That_Does_Not_Crit_Makes_No_Splinter(GearHero hero)
    {
        var f = hero.Fight(AspectFights.With(AspectFights.AmethystSplinters, 4, crit: 0.0));
        hero.CastProbe(f, AspectFights.CritUnit(hero));
        f.Wait(10);
        Assert.Empty(Splinters(f));
    }

    /// <summary>
    /// Player: a splinter does not crit, and Expertise does not raise it again: the crit already has it.
    /// Dev: 50% Expertise; the tick still equals share x S / 4 with S read from the (Expertise-boosted) crit; and the tick's crit chance is 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Splinter_Never_Crits_And_Is_Not_Scaled_Again(GearHero hero)
    {
        var f = Crit(hero, 4, expertise: 0.5);
        hero.CastProbe(f, AspectFights.CritUnit(hero));
        f.Wait(10);
        double s = FirstCritDamage(f, out _);
        var ticks = Splinters(f);
        Assert.All(ticks, t => Assert.Equal(0.0, t.CritChance));
        Assert.Equal(0.11 * s / 4.0, ticks[0].Value, 1e-6 * s);
    }

    // ---- Haste

    /// <summary>
    /// Player: haste makes the splinter tick faster and so more often in its 8 seconds: 5 ticks at 30% haste (a tick every 1.54s), each still a
    /// quarter of the share.
    /// Dev: 30% haste is off the 8-second boundary (5.2 ticks fit).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Haste_Adds_Ticks_Inside_The_Eight_Seconds(GearHero hero)
    {
        var f = Crit(hero, 4, haste: 0.3);
        hero.CastProbe(f, AspectFights.CritUnit(hero));
        f.Wait(12);
        double s = FirstCritDamage(f, out var at);
        var ticks = Splinters(f);
        Assert.Equal(5, ticks.Count);
        Assert.Equal(at.TotalSeconds + 2.0 / 1.3, ticks[0].Time.TotalSeconds, 0.002);
        Assert.All(ticks, t => Assert.Equal(0.11 * s / 4.0, t.Value, 1e-6 * s));
    }

    // ---- Targets and refresh

    /// <summary>
    /// Player: each enemy has its own splinter.
    /// Dev: two enemies, a crit on each (the second once the ability is ready again); both get ticks, the first coming 2 seconds after the first crit
    /// on that enemy. Values are not compared: area hits and Rime's swallows land on both enemies, so the shares overlap.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Each_Enemy_Has_Its_Own_Splinter(GearHero hero)
    {
        var f = Crit(hero, 4, enemies: 2);
        var second = f.Context.AllEnemies[1];
        AspectFights.CastWhenReady(f, hero, AspectFights.CritUnit(hero), f.Enemy);
        AspectFights.CastWhenReady(f, hero, AspectFights.CritUnit(hero), second);
        f.Wait(12);
        var ticks = Splinters(f);
        foreach (var enemy in new[] { f.Enemy, second })
        {
            var firstCrit = CritHits(f).First(h => h.Target == enemy.Name);
            var firstTick = ticks.First(t => t.Target == enemy.Name);
            Assert.Equal(firstCrit.Time.TotalSeconds + 2.0, firstTick.Time.TotalSeconds, 0.002);
        }
    }

    /// <summary>
    /// Player: a new crit on the same enemy restarts the 8 seconds and adds its share to what the splinter had left, the ticks keeping their rhythm.
    /// Dev: two crits on one enemy; the first tick after the second is (what the first splinter had left, in ticks' worth, + share x S2) / 4;
    /// the splinter ticks on past the first crit's 8 seconds.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_New_Crit_Restarts_The_Splinter_And_Keeps_What_Was_Left(GearHero hero)
    {
        var f = Crit(hero, 4);
        AspectFights.CastWhenReady(f, hero, AspectFights.CritUnit(hero));
        double s1 = FirstCritDamage(f, out var t1);
        int hitsBefore = CritHits(f).Count;
        AspectFights.CastWhenReady(f, hero, AspectFights.CritUnit(hero));
        var crits = CritHits(f);
        var t2 = crits[hitsBefore].Time;
        double s2 = crits.Where(h => h.Time == t2).Sum(h => h.Value);
        f.Wait(14);
        Assert.True((t2 - t1).TotalSeconds < 8.0, "the second crit comes inside the first splinter");
        var after = Splinters(f).Where(t => t.Time > t2).ToList();
        double left = 0.11 * s1 / 4.0 * (t1.TotalSeconds + 8.0 - t2.TotalSeconds) / 2.0;
        Assert.Equal((left + 0.11 * s2) / 4.0, after[0].Value, 1e-6 * s2);
        Assert.True(after.Last().Time.TotalSeconds >= t1.TotalSeconds + 8.0 - 0.002, "the splinter outlasts the first crit's 8 seconds");
        Assert.True(after.Last().Time.TotalSeconds < t2.TotalSeconds + 8.0);
    }
}
