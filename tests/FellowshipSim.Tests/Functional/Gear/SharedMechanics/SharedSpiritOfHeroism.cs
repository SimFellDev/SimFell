using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>
/// The Spirit ability, which every hero has: it costs 100 Spirit Points and grants Spirit of Heroism, +30% Haste for 20 seconds.
/// Dev: the same scenario over all six heroes; Spirit Points are set directly. Haste is read off the hero (no base haste).
/// </summary>
public class SharedSpiritOfHeroism
{
    public static IEnumerable<object[]> Heroes => SharedFights.Heroes;

    private static StepDriver Charged(GearHero hero, double points = 100)
    {
        var f = hero.Fight();
        GearFights.Fill(f);
        f.SharedSetSpirit(points);
        return f;
    }

    /// <summary>
    /// Player: the Spirit ability needs 100 Spirit Points.
    /// Dev: 99 Spirit Points are refused (and time does not move), 100 are accepted.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Spirit_Ability_Needs_A_Hundred_Spirit_Points(GearHero hero)
    {
        var f = Charged(hero, 99);
        var attempt = f.TryCast(hero.Get(f, hero.Spirit));
        Assert.False(attempt.Succeeded);
        Assert.Equal(SimTime.Zero, f.Now);

        f = Charged(hero, 100);
        Assert.True(f.TryCast(hero.Get(f, hero.Spirit)).Succeeded);
    }

    /// <summary>
    /// Player: the Spirit ability uses up the 100 Spirit Points.
    /// Dev: cast with 100; only the passive Spirit Points that came after the cast (one every 3 seconds) are left.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Spends_The_Spirit_Points(GearHero hero)
    {
        var f = Charged(hero);
        f.Cast(hero.Get(f, hero.Spirit));
        double cast = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        Assert.Equal(Math.Floor(f.Now.TotalSeconds / 3) - Math.Floor(cast / 3), f.SharedSpirit());
    }

    /// <summary>
    /// Player: Spirit of Heroism gives +30% Haste.
    /// Dev: haste before 0, after the cast 0.3.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Gives_Thirty_Percent_Haste(GearHero hero)
    {
        var f = Charged(hero);
        Assert.Equal(0.0, f.Hero.GetHaste(f.Now), 1e-9);
        f.Cast(hero.Get(f, hero.Spirit));
        Assert.Equal(0.3, f.Hero.GetHaste(f.Now), 1e-9);
    }

    /// <summary>
    /// Player: Spirit of Heroism lasts 20 seconds from the cast.
    /// Dev: up 0.1s before 20s after the cast started, gone 0.1s after. (The cast starts at 0; every Spirit ability applies it as it resolves, with
    /// at most the global cooldown's delay, so the window around 20s is the cast's end.)
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Lasts_Twenty_Seconds(GearHero hero)
    {
        var f = Charged(hero);
        f.Cast(hero.Get(f, hero.Spirit));
        var applied = f.Events.Last(e => e.Kind == StepEventKind.AuraGained && e.Name == "Spirit of Heroism").Time;
        f.Wait(applied.TotalSeconds + 19.9 - f.Now.TotalSeconds);
        Assert.True(f.Hero.Auras.HasAura(Hero.SpiritOfHeroism, f.Now));
        f.Wait(0.2);
        Assert.False(f.Hero.Auras.HasAura(Hero.SpiritOfHeroism, f.Now));
    }

    /// <summary>
    /// Player: you regain a Spirit Point every 3 seconds in combat.
    /// Dev: no Spirit Points; 9.5 seconds of waiting give 3 points, 2.5 seconds give none.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Spirit_Points_Come_Back_One_Every_Three_Seconds(GearHero hero)
    {
        var f = Charged(hero, 0);
        f.Wait(2.9);
        Assert.Equal(0, f.SharedSpirit());
        f.Wait(0.2);
        Assert.Equal(1, f.SharedSpirit());
        f.Wait(6.4);
        Assert.Equal(3, f.SharedSpirit());
    }

    /// <summary>
    /// Player: passive Spirit Points stop at 100.
    /// Dev: 100 points, 7 seconds; still 100.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Spirit_Points_Stop_At_A_Hundred(GearHero hero)
    {
        var f = Charged(hero);
        f.Wait(7);
        Assert.Equal(100, f.SharedSpirit());
    }
}
