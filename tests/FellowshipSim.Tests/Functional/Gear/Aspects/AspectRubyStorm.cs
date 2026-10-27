using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Ruby Storm (aspect 35): "Your targeted, offensive abilities have a chance (1.3 PPM) to spawn a Ruby Storm that travels forward, dealing damage to
/// all enemies it touches equal to 2.4% / 4.0% / 5.4% / 7.0% of your Current Health."
/// Dev: over the six heroes. No log has this aspect. The engine takes the hero at full health, so the damage is the share of the hero's maximum
/// health: a bare test hero has none, so each fight gives it 200,000 (<c>Stats.LoggedMaxHealth</c>). A storm is rolled once per ability cast (at its completion), not per hit. Chances from
/// <c>Rolls</c>: PPM x (1 + haste) x seconds since the last roll / 60.
/// </summary>
public class AspectRubyStorm
{
    private const string ProcKey = "ruby_storm";
    private const int StormEffect = 90;
    private static readonly double[] Share = [0.024, 0.040, 0.054, 0.070];

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> HeroRanks => AspectFights.HeroRanks;

    /// <summary>The health the hero is given (a bare test hero has none).</summary>
    private const double Health = 200_000;

    private static StepDriver Fight(GearHero hero, int rank = 1, double haste = 0, double crit = 0, double mainStat = 1000, int enemies = 1)
    {
        var f = hero.Fight(new GearLoadout { Augmentations = [(AspectFights.RubyStorm, rank)], Haste = haste, Crit = crit, MainStat = mainStat, Enemies = enemies });
        f.Hero.Stats.LoggedMaxHealth = Health;
        return f;
    }

    private static ForcedProcs Force(StepDriver f, bool outcome) => f.ForceProcs(key => key == ProcKey ? outcome : null);

    private static List<StepEvent> Storms(StepDriver f) => f.AspectEffectHits(StormEffect);

    /// <summary>
    /// Player: 1.3 procs per minute: an ability cast 30 seconds after the last roll rolls 1.3 x 30 / 60 .
    /// Dev: 20 seconds of nothing, one cast; its first roll against 1.3 x t / 60 (t the cast's completion).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Chance_Is_1_3_Procs_Per_Minute(GearHero hero)
    {
        var f = Fight(hero);
        var procs = Force(f, false);
        f.Wait(20);
        hero.CastProbe(f, hero.Unit);
        double t = f.Events.First(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        Assert.Equal(1.3 * t / 60.0, procs.Rolls.First(r => r.Key == ProcKey).Chance, 1e-9);
    }

    /// <summary>
    /// Player: haste raises the chance in proportion.
    /// Dev: 50% haste: 1.3 x 1.5 x t / 60.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Haste_Raises_The_Chance_In_Proportion(GearHero hero)
    {
        var f = Fight(hero, haste: 0.5);
        var procs = Force(f, false);
        f.Wait(20);
        hero.CastProbe(f, hero.Unit);
        double t = f.Events.First(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        Assert.Equal(1.3 * 1.5 * t / 60.0, procs.Rolls.First(r => r.Key == ProcKey).Chance, 1e-9);
    }

    /// <summary>
    /// Player: a cast rolls once, however many hits it makes.
    /// Dev: one cast of the unit hit; exactly one roll.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Cast_Rolls_Once(GearHero hero)
    {
        var f = Fight(hero);
        var procs = Force(f, false);
        hero.CastProbe(f, hero.Unit);
        f.Wait(3);
        Assert.Single(procs.Rolls, r => r.Key == ProcKey);
    }

    /// <summary>
    /// Player: no proc, no storm.
    /// Dev: the proc forced to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Proc_No_Storm(GearHero hero)
    {
        var f = Fight(hero);
        Force(f, false);
        hero.CastProbe(f, hero.Unit);
        f.Wait(3);
        Assert.Empty(Storms(f));
    }

    /// <summary>
    /// Player: the storm deals the rank's share (2.4% / 4.0% / 5.4% / 7.0%) of your health.
    /// Dev: the proc forced; the first storm against share x 200,000.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanks))]
    public void The_Storm_Deals_The_Rank_Share_Of_Health(GearHero hero, int rank)
    {
        var f = Fight(hero, rank);
        Force(f, true);
        hero.CastProbe(f, hero.Unit);
        Assert.Equal(Share[rank - 1] * Health, Storms(f)[0].Value, 1e-6);
    }

    /// <summary>
    /// Player: the storm scales with nothing but your health: not with Main Stat, and it does not crit.
    /// Dev: 5000 Main Stat and 100% crit chance: the same damage and a crit chance of 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Storm_Does_Not_Scale_With_Main_Stat_Or_Crit(GearHero hero)
    {
        var plain = Fight(hero, 4);
        Force(plain, true);
        hero.CastProbe(plain, hero.Unit);
        var strong = Fight(hero, 4, crit: 1.0, mainStat: 5000);
        Force(strong, true);
        hero.CastProbe(strong, hero.Unit);
        Assert.Equal(Storms(plain)[0].Value, Storms(strong)[0].Value, 1e-6);
        Assert.Equal(0.0, Storms(strong)[0].CritChance);
    }

    /// <summary>
    /// Player: the storm touches every enemy.
    /// Dev: three enemies: one storm hit on each, all equal.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Storm_Hits_Every_Enemy(GearHero hero)
    {
        var f = Fight(hero, enemies: 3);
        Force(f, true);
        hero.CastProbe(f, hero.Unit);
        var storms = Storms(f);
        Assert.Equal(3, storms.Count);
        Assert.Equal(3, storms.Select(s => s.Target).Distinct().Count());
        Assert.All(storms, s => Assert.Equal(storms[0].Value, s.Value, 1e-6));
    }
}
