using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Vengeful Soul (sigil 5): "Your critical strikes have a chance to increase your Main Stat by 3.0% / 5.1% / 7.2% / 9.3% for 8 seconds. If you are
/// below 50% health when Vengeful Soul triggers, you are also healed ... (3.0 PPM)"
/// Dev: the proc is forced; the hero has 100% crit and the dice roll crits, so each hit is a crit (or no crit chance, for the negative case). The healing
/// does not affect damage and is not tested. Run over every hero.
/// </summary>
public class SigilVengefulSoul
{
    private const int Sigil = 5;
    private const double ExpiryLag = 0.05;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(0.03, 0.051, 0.072, 0.093);

    private static StepDriver CritFight(GearHero hero, int rank = 1)
    {
        var f = SigilFights.Fight(hero, Sigil, rank, crit: 1.0);
        f.CritsRoll();
        return f;
    }

    /// <summary>
    /// Player: a critical strike that procs raises your Main Stat by 3.0% / 5.1% / 7.2% / 9.3% by rank.
    /// Dev: the Main Stat ratio against the hero's Main Stat before the cast.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void A_Proc_Raises_Main_Stat_By_Rank(GearHero hero, int rank, double bonus)
    {
        var f = CritFight(hero, rank);
        double before = f.MainStat();
        f.Force("vengeful_soul", 1);
        f.CastCrittable(hero);
        Assert.Equal(before * (1 + bonus), f.MainStat(), before * 1e-6);
    }

    /// <summary>
    /// Player: the buff lasts 8 seconds.
    /// Dev: one forced proc; the buff is removed 8s after it was gained, within a server frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Buff_Lasts_Eight_Seconds(GearHero hero)
    {
        var f = CritFight(hero);
        f.Force("vengeful_soul", 1);
        f.CastCrittable(hero);
        f.Wait(9);
        Assert.Equal(8.0, f.RemovedAt("Vengeful Soul")[0] - f.GainedAt("Vengeful Soul")[0], ExpiryLag);
    }

    /// <summary>
    /// Player: it rolls on critical strikes only; a hit that does not crit does not roll.
    /// Dev: a hero with no crit chance casts and the proc is never rolled.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Hits_That_Are_Not_Critical_Strikes_Do_Not_Roll(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1, crit: 0.0);
        f.CritsRoll();
        var procs = f.Force("vengeful_soul");
        f.CastCrittable(hero);
        Assert.DoesNotContain(procs.Rolls, r => r.Key == "vengeful_soul");
        Assert.Equal(1000.0, f.MainStat(), 1e-6);
    }

    /// <summary>
    /// Player: a critical strike rolls, and the chance is 3.0 procs per minute, more with haste.
    /// Dev: a crit after 5s without a roll: the chance given is 3.0 x (1 + haste) x (the hit's time) / 60.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Chance_Is_3_Procs_Per_Minute(double haste)
    {
        var hero = GearFights.Heroes[0];
        var f = SigilFights.Fight(hero, Sigil, 1, crit: 1.0, haste: haste);
        f.CritsRoll();
        var procs = f.Force("vengeful_soul", 0);
        f.Wait(5);
        f.CastCrittable(hero);
        var roll = procs.Rolls.First(r => r.Key == "vengeful_soul");
        // The bolt lands a moment after the cast completes (travel time), so the chance is read against the first damage event.
        double hitAt = f.Events.First(e => e.Kind == StepEventKind.Damage).Time.TotalSeconds;
        Assert.Equal(3.0 * (1 + haste) * hitAt / 60.0, roll.Chance, 0.0005);
    }
}
