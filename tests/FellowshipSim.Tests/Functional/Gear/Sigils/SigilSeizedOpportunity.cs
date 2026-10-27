using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Seized Opportunity (sigil 4): "Every 20 Critical Strikes you gain 21 / 39 / 57 / 75 Critical Strike rating for 12 seconds. You can not gain stacks
/// while Seized Opportunity is active on you."
/// Dev: the hero has 100% crit and the dice roll crits (<see cref="SigilFights.CritsRoll"/>), so every hit is a crit; the crits are counted from the hero's
/// damage events. The buff is read as the hero's crit chance above its 100% (a rating is 0.16%; the 75 of rank 4 is past the 10% breakpoint, where
/// diminishing returns take 2%: 0.10 + 0.02 x 0.98 = 11.96%). Run over every hero.
/// </summary>
public class SigilSeizedOpportunity
{
    private const int Sigil = 4;
    private const double ExpiryLag = 0.05;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(0.0336, 0.0624, 0.0912, 0.1196);

    private static StepDriver CritFight(GearHero hero, int rank, out Func<int> crits)
    {
        var f = SigilFights.Fight(hero, Sigil, rank, crit: 1.0);
        f.CritsRoll();
        int count = 0;
        f.Hero.OnDamageDealt += (in DamageContext hit) => { if (hit.IsCrit) count++; };
        crits = () => count;
        return f;
    }

    /// <summary>Casts Basic abilities until the crit count reaches <paramref name="count"/>, and returns the buff gain's crit chance above 100%.</summary>
    private static void CritUntil(StepDriver f, GearHero hero, Func<int> crits, int count)
    {
        for (int casts = 0; crits() < count; casts++)
        {
            Assert.True(casts < 100, $"{hero.Name}: {crits()} crits after 100 casts");
            f.CastCrittable(hero);
        }
    }

    /// <summary>
    /// Player: the buff comes on the 20th Critical Strike and not before.
    /// Dev: after every cast the buff is up exactly when 20 crits have landed (a cast with several hits can cross 20).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Twentieth_Critical_Strike_Grants_The_Buff(GearHero hero)
    {
        var f = CritFight(hero, 1, out var crits);
        for (int casts = 0; crits() < 20; casts++)
        {
            Assert.True(casts < 100, $"{hero.Name}: {crits()} crits after 100 casts");
            f.CastCrittable(hero);
            bool buffed = f.Hero.CritChance(f.Now) > 1.0 + 1e-9;
            Assert.Equal(crits() >= 20, buffed);
        }
    }

    /// <summary>
    /// Player: the buff is 21 / 39 / 57 / 75 Critical Strike rating by rank.
    /// Dev: the crit chance above the sheet's 100% once 20 crits have landed.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void The_Buff_Is_Crit_Rating_By_Rank(GearHero hero, int rank, double critChance)
    {
        var f = CritFight(hero, rank, out var crits);
        CritUntil(f, hero, crits, 20);
        Assert.Equal(1.0 + critChance, f.Hero.CritChance(f.Now), 1e-6);
    }

    /// <summary>
    /// Player: the buff lasts 12 seconds.
    /// Dev: the crit counter is removed as the buff is gained; the buff is removed 12s later, within a server frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Buff_Lasts_Twelve_Seconds(GearHero hero)
    {
        var f = CritFight(hero, 1, out var crits);
        CritUntil(f, hero, crits, 20);
        f.Wait(13);
        var removed = f.RemovedAt("Seized Opportunity");
        Assert.True(removed.Count >= 2);
        Assert.Equal(12.0, removed[1] - removed[0], ExpiryLag);
        Assert.Equal(1.0, f.Hero.CritChance(f.Now), 1e-9);
    }

    /// <summary>
    /// Player: no stacks are gained while the buff is on you.
    /// Dev: a crit while the buff is up leaves the counter alone (no further gain of the aura). One cast only: Tariq's Face Breaker has a cooldown that would outlast the 12s.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Stacks_Are_Gained_While_The_Buff_Is_Active(GearHero hero)
    {
        var f = CritFight(hero, 1, out var crits);
        CritUntil(f, hero, crits, 20);
        int gains = f.GainedAt("Seized Opportunity").Count;
        f.CastCrittable(hero);
        Assert.Equal(gains, f.GainedAt("Seized Opportunity").Count);
    }

    /// <summary>
    /// Player: only Critical Strikes count.
    /// Dev: forty hits with no crit chance leave the hero without the buff.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Hits_That_Are_Not_Critical_Strikes_Do_Not_Count(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1, crit: 0.0);
        f.CritsRoll();
        for (int i = 0; i < 25; i++) f.CastCrittable(hero);
        Assert.Empty(f.GainedAt("Seized Opportunity"));
    }
}
