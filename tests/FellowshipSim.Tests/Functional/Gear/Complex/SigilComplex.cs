using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Sigils with something else: Willful Momentum with Spirit Refund and Rising Spirit, and two Main Stat sigils together.
/// Dev: Spirit Refund is forced (<see cref="SigilFights.Force(StepDriver, string, int)"/> on "spirit_refund") and the hero has some Spirit so the roll
/// happens; a refund is caused by the hero's Power ability (which spends its resource; Gunde's comes from an ability that applies Rend). Main Stat is read
/// off the hero. Every hero.
/// </summary>
public class SigilComplex
{
    private const double ExpiryLag = 0.05;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> WillfulByRank => SigilFights.HeroesByRank(0.015, 0.026, 0.037, 0.048);

    private static StepDriver WithWillfulMomentum(GearHero hero, int rank) =>
        SigilFights.Fight(hero, 39, rank, spirit: 0.2);

    /// <summary>The Power ability, its cooldown cleared first so it can be used again (Rupture's is a minute).</summary>
    private static void Refund(StepDriver f, GearHero hero)
    {
        f.Hero.ReduceCooldown(hero.Get(f, hero.Power).Name, SimTime.FromSeconds(1000), f.Now);
        hero.CastProbe(f, hero.Power);
    }

    /// <summary>
    /// Player: a Spirit Refund raises your Main Stat by 1.5% / 2.6% / 3.7% / 4.8% (by rank) for 4 seconds, on top of the Rising Spirit it also gives.
    /// Dev: a Power ability with the refund forced; Main Stat afterwards over the 1000 before. Rising Spirit (+5% a stack, in its own sum) and
    /// the sigil's bonus multiply, as Hero.PrimaryAttribute documents: 1000 x 1.05 x (1 + bonus).
    /// </summary>
    [Theory]
    [MemberData(nameof(WillfulByRank))]
    public void A_Spirit_Refund_Raises_Main_Stat_By_Rank(GearHero hero, int rank, double bonus)
    {
        var f = WithWillfulMomentum(hero, rank);
        f.Force("spirit_refund", 1);
        Refund(f, hero);
        Assert.Single(f.GainedAt("Rising Spirit"));
        Assert.Equal(1000.0 * 1.05 * (1 + bonus), f.MainStat(), 1e-3);
    }

    /// <summary>
    /// Player: the bonus lasts 4 seconds.
    /// Dev: one forced refund; the buff is removed 4s after it was gained, within a server frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Bonus_Lasts_Four_Seconds(GearHero hero)
    {
        var f = WithWillfulMomentum(hero, 1);
        f.Force("spirit_refund", 1);
        Refund(f, hero);
        f.Wait(5);
        Assert.Equal(4.0, f.RemovedAt("Willful Momentum")[0] - f.GainedAt("Willful Momentum")[0], ExpiryLag);
    }

    /// <summary>
    /// Player: without a Spirit Refund there is no bonus.
    /// Dev: the same cast with the refund failing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Refund_Means_No_Bonus(GearHero hero)
    {
        var f = WithWillfulMomentum(hero, 1);
        f.Force("spirit_refund", 0);
        Refund(f, hero);
        Assert.Empty(f.GainedAt("Willful Momentum"));
        Assert.Equal(1000.0, f.MainStat(), 1e-6);
    }

    /// <summary>
    /// Player: another refund while the bonus is on restarts it; it does not stack.
    /// Dev: two refunds; the Main Stat has the bonus once, and the buff is removed 4s after the second gain.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Second_Refund_Restarts_The_Bonus_Without_Stacking(GearHero hero)
    {
        var f = WithWillfulMomentum(hero, 1);
        f.Force("spirit_refund");
        Refund(f, hero);
        f.Wait(1);
        Refund(f, hero);
        // Rising Spirit stacks (5% a stack); the sigil's 1.5% does not.
        int stacks = 2;
        Assert.Equal(1000.0 * (1 + 0.05 * stacks) * 1.015, f.MainStat(), 1e-3);
        f.Wait(6);
        var gains = f.GainedAt("Willful Momentum");
        Assert.Equal(4.0, f.RemovedAt("Willful Momentum").Last() - gains.Last(), ExpiryLag);
    }

    /// <summary>
    /// Player: Main Stat bonuses of the "increased" kind from different sigils add up (Hidden Power's Power Revealed with Vengeful Soul).
    /// Dev: rank 1 of each (+7.2% and +3.0%) gives 1000 x (1 + 0.072 + 0.030), as Hero.PrimaryAttribute documents.
    /// TODO: check in game that the two do not multiply (1.0722 x 1.03 = 1104.2 against 1102).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Increased_Main_Stat_Sigils_Add_Up(GearHero hero)
    {
        var f = hero.Fight(new GearLoadout { Augmentations = [(1, 1), (5, 1)], Crit = 1.0 });
        f.CritsRoll();
        // Power Revealed from five stacks (every cast rolls), then a Vengeful Soul proc is forced on the crit.
        var procs = f.ForceProcs(k => k is "hidden_power" or "vengeful_soul" ? true : false);
        for (int i = 0; i < 5; i++) f.CastCrittable(hero);
        Assert.NotEmpty(f.GainedAt("Power Revealed"));
        Assert.NotEmpty(f.GainedAt("Vengeful Soul"));
        Assert.NotEmpty(procs.Rolls);
        Assert.Equal(1000.0 * (1 + 0.072 + 0.030), f.MainStat(), 1e-3);
    }
}
