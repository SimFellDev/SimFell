using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Kindling (sigil 28): "Your Damage Effects have a chance (2.1 PPM) to burn the target, dealing 483% / 882% / 1281% / 1680% of your Main Stat in fire
/// damage to them over 9 seconds." Each application of a harmful effect rolls (a tick of a damage over time does not); a hit rolls too. The burn ticks every
/// 1.5 seconds shortened by haste, with no tick on application and a partial tick at the end; a new proc on a burning enemy restarts the 9 seconds and keeps
/// the tick rhythm. Each tick is 0.69 / 1.26 / 1.83 / 2.40 x Main Stat, and can crit and gains from Expertise.
/// Dev: the proc is forced; the burn's ticks are the damage events of effect 1000104 (the effect's ID plus the 1,000,000 of logged effects). Run over
/// every hero with its unit hit, which is the roll.
/// </summary>
public class SigilKindling
{
    private const int Sigil = 28;
    private const int TickId = 1_000_104;
    private const double Tolerance = 0.002;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(0.69, 1.26, 1.83, 2.40);

    private static List<StepEvent> Ticks(StepDriver f) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId == TickId).ToList();

    /// <summary>A hero that has ignited its enemy with its unit hit; returns the time of the hit.</summary>
    private static StepDriver Ignited(GearHero hero, out double hitAt, int rank = 1, double haste = 0.0, double crit = 0.0, double expertise = 0.0, int enemies = 1)
    {
        var f = hero.Fight(new GearLoadout { Augmentations = [(Sigil, rank)], Haste = haste, Crit = crit, Expertise = expertise, Enemies = enemies });
        f.Force("kindling", 1);
        GearFights.Fill(f);
        f.Cast(hero.Get(f, hero.Unit));
        hitAt = f.Events.First(e => e.Kind == StepEventKind.Damage && e.AbilityId != TickId).Time.TotalSeconds;
        return f;
    }

    /// <summary>
    /// Player: a hit that procs burns the enemy; the first tick comes one tick period (1.5 seconds) later, not on application.
    /// Dev: the first tick's time minus the hit's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Burn_Has_No_Tick_On_Application(GearHero hero)
    {
        var f = Ignited(hero, out double hitAt);
        f.Wait(12);
        Assert.NotEmpty(Ticks(f));
        Assert.Equal(1.5, Ticks(f)[0].Time.TotalSeconds - hitAt, Tolerance);
    }

    /// <summary>
    /// Player: the burn ticks every 1.5 seconds, shortened by haste.
    /// Dev: gaps between the first ticks at 0, 25 and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0, 1.5)]
    [InlineData(0.25, 1.2)]
    [InlineData(0.5, 1.0)]
    public void The_Burn_Ticks_Every_One_And_A_Half_Seconds_Shortened_By_Haste(double haste, double period)
    {
        var hero = GearFights.Heroes[0];
        var f = Ignited(hero, out _, haste: haste);
        f.Wait(12);
        var ticks = Ticks(f);
        for (int i = 1; i < 4; i++) Assert.Equal(period, ticks[i].Time.TotalSeconds - ticks[i - 1].Time.TotalSeconds, Tolerance);
    }

    /// <summary>
    /// Player: the burn lasts 9 seconds, six ticks at 1.5 seconds; haste does not lengthen it, it adds ticks.
    /// Dev: the damage of all the ticks is 9 / 1.5 x (1 + haste) ticks' worth (a partial one at the end counts by its share), at 0 and 20% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Burn_Deals_Nine_Seconds_Of_Ticks(double haste)
    {
        var hero = GearFights.Heroes[0];
        var f = Ignited(hero, out double hitAt, haste: haste);
        f.Wait(20);
        double tick = 0.69 * 1000 * SigilFights.ClassTuning(hero);
        Assert.Equal(9.0 / 1.5 * (1 + haste) * tick, Ticks(f).Sum(t => t.Value), tick * 0.05);
        Assert.True(Ticks(f).Last().Time.TotalSeconds - hitAt <= 9.0 + 0.05);
    }

    /// <summary>
    /// Player: each tick deals 0.69 / 1.26 / 1.83 / 2.40 x your Main Stat by rank (the tooltip's 483% / 882% / 1281% / 1680% are 7 ticks of them).
    /// Dev: the first tick against the Main Stat of 1000, no Expertise and no crit, times the hero's class damage tuning (Rime 1.04, Ardeos 1.10, Elarion 1.08, Mara 1.04).
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void A_Tick_Is_A_Share_Of_Main_Stat_By_Rank(GearHero hero, int rank, double coefficient)
    {
        var f = Ignited(hero, out _, rank: rank);
        f.Wait(3);
        double expected = coefficient * 1000 * SigilFights.ClassTuning(hero);
        Assert.Equal(expected, Ticks(f)[0].Value, expected * 0.001);
    }

    /// <summary>
    /// Player: the tooltip's total, 483% of Main Stat at rank 1 over the 9 seconds, is what a burn deals.
    /// Dev: the tick damage over 9s without haste. The tooltip's 483% is seven ticks of 0.69, while 9 seconds at 1.5 seconds a tick is six.
    /// TODO: check in game whether the burn has six ticks (414%) or seven (483%).
    /// </summary>
    [Fact(Skip = "The tooltip's 483% is 7 ticks of 0.69, but 9s at 1.5s is 6 ticks: how many ticks the game's burn has is not settled; to check in game")]
    public void The_Burn_Deals_The_Tooltips_Total()
    {
        var hero = GearFights.Heroes[0];
        var f = Ignited(hero, out _);
        f.Wait(12);
        Assert.Equal(4.83 * 1000, Ticks(f).Sum(t => t.Value), 5.0);
    }

    /// <summary>
    /// Player: the burn's ticks gain from Expertise.
    /// Dev: a tick at 10% Expertise over a tick at none.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Ticks_Gain_From_Expertise(GearHero hero)
    {
        double Tick(double expertise)
        {
            var f = Ignited(hero, out _, expertise: expertise);
            f.Wait(3);
            return Ticks(f)[0].Value;
        }
        Assert.Equal(1.1, Tick(0.1) / Tick(0.0), 0.001);
    }

    /// <summary>
    /// Player: the burn's ticks can crit, with the hero's crit chance.
    /// Dev: the first tick's crit chance for a hero with 30% crit.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Ticks_Can_Crit(GearHero hero)
    {
        var f = Ignited(hero, out _, crit: 0.3);
        f.Wait(3);
        Assert.Equal(0.3, Ticks(f)[0].CritChance, 1e-6);
    }

    /// <summary>
    /// Player: a tick of the burn does not roll for another burn; neither does a tick of another damage over time.
    /// Dev: with no further hits, the 9 seconds of ticks add no roll after the one for the hit.
    /// TODO: fails for Ardeos and Gunde on purpose (a possible engine bug): the ticks of their own damage over time (Fire Ball's burn, Rend) roll for
    /// Kindling, because only gear effects register their ticks as damage over time. The game data says a tick does not roll.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Ticks_Do_Not_Roll(GearHero hero)
    {
        var f = hero.Fight(new GearLoadout { Augmentations = [(Sigil, 1)] });
        var procs = f.Force("kindling", 1);
        GearFights.Fill(f);
        f.Cast(hero.Get(f, hero.Unit));
        f.Wait(1);
        int rolls = procs.Rolls.Count(r => r.Key == "kindling");
        f.Wait(12);
        Assert.Equal(rolls, procs.Rolls.Count(r => r.Key == "kindling"));
    }

    /// <summary>
    /// Player: applying a harmful effect to an enemy rolls for the burn too (not only damage).
    /// Dev: the hero's harmful-effect application is raised on its own (no hit), with the proc forced: the burn runs.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void An_Effect_Application_Rolls(GearHero hero)
    {
        var f = hero.Fight(new GearLoadout { Augmentations = [(Sigil, 1)] });
        var procs = f.Force("kindling", 1);
        f.Hero.TriggerHarmfulEffectApplied(f.Context, f.Enemy);
        Assert.Contains(procs.Rolls, r => r.Key == "kindling");
        f.Wait(12);
        Assert.NotEmpty(Ticks(f));
    }

    /// <summary>
    /// Player: a new proc on a burning enemy restarts the 9 seconds and keeps the ticks' rhythm.
    /// Dev: a second proc 3.5s in (no haste): the ticks go on every 1.5s from the first hit, and the last is 9s after the second proc, not the first.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_New_Proc_Restarts_The_Duration_And_Keeps_The_Rhythm(GearHero hero)
    {
        var f = Ignited(hero, out double hitAt);
        bool armed = false;
        f.Force("kindling", () => armed);
        f.Wait(3.5 - (f.Now.TotalSeconds - hitAt));
        armed = true;
        f.Hero.TriggerHarmfulEffectApplied(f.Context, f.Enemy);
        armed = false;
        double secondAt = f.Now.TotalSeconds;
        f.Wait(20);
        var ticks = Ticks(f);
        // Ticks stay on the first hit's grid of 1.5s.
        foreach (var t in ticks.Take(ticks.Count - 1))
        {
            double grid = (t.Time.TotalSeconds - hitAt) / 1.5;
            Assert.Equal(Math.Round(grid), grid, 0.01);
        }
        // The burn outlasts the first 9 seconds.
        Assert.True(ticks.Last().Time.TotalSeconds > hitAt + 9.5);
        Assert.Equal(secondAt + 9.0, ticks.Last().Time.TotalSeconds, 0.1);
    }

    /// <summary>
    /// Player: each enemy has its own burn.
    /// Dev: two enemies; a proc on each: both take ticks.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Each_Enemy_Burns_On_Its_Own(GearHero hero)
    {
        var f = hero.Fight(new GearLoadout { Augmentations = [(Sigil, 1)], Enemies = 2 });
        f.Force("kindling", 2);
        f.Hero.TriggerHarmfulEffectApplied(f.Context, f.Context.AllEnemies[0]);
        f.Hero.TriggerHarmfulEffectApplied(f.Context, f.Context.AllEnemies[1]);
        f.Wait(12);
        var byTarget = Ticks(f).GroupBy(t => t.Target).ToList();
        Assert.Equal(2, byTarget.Count);
        Assert.Equal(byTarget[0].Count(), byTarget[1].Count());
    }

    /// <summary>
    /// Player: the chance is 2.1 procs per minute, more with haste, rolled by each hit.
    /// Dev: a hit after 5s without a roll: the chance given is 2.1 x (1 + haste) x (the hit's time) / 60.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Chance_Is_2_1_Procs_Per_Minute(double haste)
    {
        var hero = GearFights.Heroes[0];
        var f = hero.Fight(new GearLoadout { Augmentations = [(Sigil, 1)], Haste = haste });
        var procs = f.Force("kindling", 0);
        f.Wait(5);
        GearFights.Fill(f);
        f.Cast(hero.Get(f, hero.Unit));
        double hitAt = f.Events.First(e => e.Kind == StepEventKind.Damage).Time.TotalSeconds;
        var roll = procs.Rolls.First(r => r.Key == "kindling");
        Assert.Equal(2.1 * (1 + haste) * hitAt / 60.0, roll.Chance, 0.0005);
    }
}
