using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Hidden Power (sigil 1): "Your abilities have a chance to grant you 1 stack of Hidden Power for 60 seconds. When you have 5 stacks, Hidden Power is
/// consumed and Power Revealed is granted to you for 15 seconds, increasing your Main Stat by 7.2% / 12.8% / 18.4% / 24%. You can not gain Hidden Power
/// stacks while Power Revealed is active on you. (2.6 PPM)"
/// Dev: the proc is forced (<see cref="SigilFights.Force(StepDriver, string, int)"/>), so each cast of a hero's Basic ability is one roll; the main stat is read
/// off the hero. Run over every hero.
/// </summary>
public class SigilHiddenPower
{
    private const int Sigil = 1;
    /// <summary>An aura is removed up to a server frame (about 17ms) after its duration (combat logs).</summary>
    private const double ExpiryLag = 0.05;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(0.072, 0.128, 0.184, 0.24);

    /// <summary>
    /// Player: every ability use rolls for a stack, and the stack lasts 60 seconds.
    /// Dev: one forced proc on the first cast; the stack is gained at that cast and removed 60s later.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Stack_Lasts_Sixty_Seconds(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Force("hidden_power", 1);
        f.CastBasic(hero);
        f.Wait(61);
        var gained = f.GainedAt("Hidden Power");
        var removed = f.RemovedAt("Hidden Power");
        Assert.Single(gained);
        Assert.Single(removed);
        Assert.Equal(60.0, removed[0] - gained[0], ExpiryLag);
    }

    /// <summary>
    /// Player: the chance is 2.6 procs per minute, more with haste.
    /// Dev: a cast after 5s without any: the chance given is 2.6 x (1 + haste) x (the cast's completion time) / 60 at the cast's completion.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Chance_Is_2_6_Procs_Per_Minute(double haste)
    {
        var hero = GearFights.Heroes[0];
        var f = SigilFights.Fight(hero, Sigil, 1, haste: haste);
        var procs = f.Force("hidden_power", 0);
        f.Wait(5);
        f.CastBasic(hero);
        var chance = procs.Rolls.First(r => r.Key == "hidden_power").Chance;
        Assert.Equal(2.6 * (1 + haste) * f.LastCastCompletedAt() / 60.0, chance, 0.0005);
    }

    /// <summary>
    /// Player: five stacks consume themselves and grant Power Revealed, +7.2% / 12.8% / 18.4% / 24% Main Stat by rank.
    /// Dev: five forced procs on five Basic casts; the Main Stat ratio against the same hero before the fifth stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void Five_Stacks_Grant_Power_Revealed(GearHero hero, int rank, double bonus)
    {
        var f = SigilFights.Fight(hero, Sigil, rank);
        f.Force("hidden_power");
        double before = f.MainStat();
        for (int i = 0; i < 4; i++) f.CastBasic(hero);
        Assert.Equal(before, f.MainStat(), 1e-6);
        Assert.Empty(f.GainedAt("Power Revealed"));
        f.CastBasic(hero);
        Assert.Single(f.GainedAt("Power Revealed"));
        Assert.Equal(before * (1 + bonus), f.MainStat(), before * 1e-6);
        Assert.Single(f.RemovedAt("Hidden Power").TakeLast(1));
    }

    /// <summary>
    /// Player: Power Revealed lasts 15 seconds.
    /// Dev: read from the aura's gain and removal times.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Power_Revealed_Lasts_Fifteen_Seconds(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Force("hidden_power", 5);
        for (int i = 0; i < 5; i++) f.CastBasic(hero);
        f.Wait(16);
        Assert.Equal(15.0, f.RemovedAt("Power Revealed")[0] - f.GainedAt("Power Revealed")[0], ExpiryLag);
    }

    /// <summary>
    /// Player: no Hidden Power stacks are gained while Power Revealed is on you.
    /// Dev: with every proc succeeding, casts during Power Revealed add no stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Stacks_Are_Gained_While_Power_Revealed_Is_Active(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Force("hidden_power");
        for (int i = 0; i < 5; i++) f.CastBasic(hero);
        int gainsBefore = f.GainedAt("Hidden Power").Count;
        f.CastBasic(hero);
        f.CastBasic(hero);
        Assert.Equal(gainsBefore, f.GainedAt("Hidden Power").Count);
    }

    /// <summary>
    /// Player: when Power Revealed ends, stacks can be gained again.
    /// Dev: after the 15s, a forced cast gives a first stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Stacks_Are_Gained_Again_After_Power_Revealed_Ends(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Force("hidden_power");
        for (int i = 0; i < 5; i++) f.CastBasic(hero);
        f.Wait(16);
        int gainsBefore = f.GainedAt("Hidden Power").Count;
        f.CastBasic(hero);
        Assert.Equal(gainsBefore + 1, f.GainedAt("Hidden Power").Count);
    }

    /// <summary>
    /// Player: any ability can roll for a stack, not only damaging ones (the Spirit ability, a Major ability).
    /// Dev: a forced proc on each hero's Major ability gives a stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Every_Category_Of_Ability_Rolls(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        var procs = f.Force("hidden_power");
        hero.CastProbe(f, hero.Major);
        Assert.NotEmpty(f.GainedAt("Hidden Power"));
        Assert.NotEmpty(procs.Rolls);
    }
}
