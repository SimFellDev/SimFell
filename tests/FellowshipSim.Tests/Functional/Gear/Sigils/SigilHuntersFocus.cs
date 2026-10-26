using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Hunter's Focus (sigil 42): "Your targeted offensive abilities grant you 4 / 8 / 12 / 16 Haste rating for 8 seconds, stacking up to 5 times. The Haste
/// is lost if you use a targeted offensive ability on any other enemy."
/// Dev: Basic abilities are the targeted offensive abilities; haste is read off the hero (a rating is 0.16% haste, below the 10% breakpoint where
/// diminishing returns start). Run over every hero.
/// </summary>
public class SigilHuntersFocus
{
    private const int Sigil = 42;
    private const double ExpiryLag = 0.05;
    private const string Buff = "Trait : Focused Haste";

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(4 * 0.0016, 8 * 0.0016, 12 * 0.0016, 16 * 0.0016);

    /// <summary>
    /// Player: a targeted offensive ability grants 4 / 8 / 12 / 16 Haste rating by rank.
    /// Dev: haste after one Basic ability (no other haste on the hero) is rating x 0.16%.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void An_Ability_Grants_Haste_Rating_By_Rank(GearHero hero, int rank, double haste)
    {
        var f = SigilFights.Fight(hero, Sigil, rank);
        Assert.Equal(0.0, f.Haste(), 1e-9);
        f.CastBasic(hero);
        Assert.Equal(haste, f.Haste(), 1e-6);
    }

    /// <summary>
    /// Player: the haste stacks up to 5 times.
    /// Dev: rank 1 (4 rating a stack), six Basic casts at 100% crit-free damage; haste after each, and the sixth adds nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Stacks_Up_To_Five_Times(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        double[] seen = new double[6];
        for (int i = 0; i < 6; i++)
        {
            f.CastBasic(hero);
            seen[i] = f.Haste();
        }
        // The stacks are read as haste: 4 rating (0.64%) each, the cast's own timing does not matter.
        for (int i = 0; i < 5; i++) Assert.Equal((i + 1) * 4 * 0.0016, seen[i], 1e-6);
        Assert.Equal(5 * 4 * 0.0016, seen[5], 1e-6);
    }

    /// <summary>
    /// Player: the haste lasts 8 seconds (a new stack restarts them).
    /// Dev: one cast; the buff is removed 8s after it was gained, within a server frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Haste_Lasts_Eight_Seconds(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.CastBasic(hero);
        f.Wait(9);
        Assert.Equal(8.0, f.RemovedAt(Buff).Last() - f.GainedAt(Buff).Last(), ExpiryLag);
        Assert.Equal(0.0, f.Haste(), 1e-9);
    }

    /// <summary>
    /// Player: using a targeted offensive ability on another enemy loses the haste (and starts again from one stack).
    /// Dev: two Basic casts on the first enemy, one on the second: one stack left.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Another_Target_Loses_The_Stacks(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1, enemies: 2);
        f.CastBasic(hero);
        f.CastBasic(hero);
        Assert.Equal(2 * 4 * 0.0016, f.Haste(), 1e-6);
        f.CastBasic(hero, f.Context.AllEnemies[1]);
        Assert.Equal(4 * 0.0016, f.Haste(), 1e-6);
    }

    /// <summary>
    /// Player: the stacks stay while the hero keeps to one enemy.
    /// Dev: casts on the first enemy only keep three stacks after three casts.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Same_Target_Keeps_The_Stacks(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1, enemies: 2);
        for (int i = 0; i < 3; i++) f.CastBasic(hero);
        Assert.Equal(3 * 4 * 0.0016, f.Haste(), 1e-6);
    }

    /// <summary>
    /// Player: only a targeted offensive ability gives a stack; a self-targeted or non-damaging ability does not.
    /// Dev: a Major ability on a fresh hero.
    /// TODO: check in game what counts. The engine takes any ability with a damage coefficient, on the cast completing; the game is thought to count
    /// only successful casts of targeted abilities, and what "targeted" covers (area abilities, effect ticks, abilities that pick their target) is not settled.
    /// </summary>
    [Theory(Skip = "What counts as a targeted offensive ability is not settled (engine: any ability with a damage coefficient); to check in game")]
    [MemberData(nameof(Heroes))]
    public void Only_Targeted_Offensive_Abilities_Count(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        hero.CastProbe(f, hero.Major);
        Assert.Equal(0.0, f.Haste(), 1e-9);
    }
}
