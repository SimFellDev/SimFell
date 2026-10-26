using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Patient Soul (sigil 46): "Standing still for 3 seconds grants you Patient Soul, increasing your Max Health by 5% and your Expertise rating by
/// 12 / 22 / 32 / 42. Patient Soul persists for 6 seconds after you start moving."
/// Dev: the engine does not simulate movement, so a hero stands still for the whole fight and has the buff from the start (a replay follows the log).
/// Expertise is read off the hero (a rating is 0.16%). Run over every hero. The Max Health part does not affect damage and is not tested.
/// </summary>
public class SigilPatientSoul
{
    private const int Sigil = 46;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(12 * 0.0016, 22 * 0.0016, 32 * 0.0016, 42 * 0.0016);

    /// <summary>
    /// Player: the hero's Expertise is raised by 12 / 22 / 32 / 42 rating by rank while standing still.
    /// Dev: Expertise at the start of the fight, a rating being 0.16%.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void It_Grants_Expertise_Rating_By_Rank(GearHero hero, int rank, double expertise)
    {
        var f = SigilFights.Fight(hero, Sigil, rank);
        Assert.Equal(expertise, f.Hero.Expertise(f.Now), 1e-6);
    }

    /// <summary>
    /// Player: the Expertise shows in damage: a hit does (1 + Expertise) times more.
    /// Dev: the same hit with and without the sigil, rank 1.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Expertise_Raises_Damage(GearHero hero)
    {
        double Hit(bool sigil)
        {
            var f = sigil ? SigilFights.Fight(hero, Sigil, 1) : hero.Fight();
            f.CastBasic(hero);
            return f.Events.First(e => e.Kind == StepEventKind.Damage).Value;
        }
        Assert.Equal(1.0 + 12 * 0.0016, Hit(true) / Hit(false), 0.001);
    }

    /// <summary>
    /// Player: Patient Soul stays on while the hero stands still, however long the fight.
    /// Dev: Expertise after a minute without moving.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Stays_While_Standing_Still(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Wait(60);
        Assert.Equal(12 * 0.0016, f.Hero.Expertise(f.Now), 1e-6);
    }

    /// <summary>
    /// Player: it takes 3 seconds of standing still to gain Patient Soul, and it lasts 6 seconds after moving.
    /// Dev: not simulated; the engine has the buff from the first moment and never loses it.
    /// TODO: movement is not part of the sim. Check in game; a replay follows the log's own buff times.
    /// </summary>
    [Fact(Skip = "Movement is not simulated: the 3s to gain it and the 6s after moving cannot be tested in a step fight")]
    public void It_Needs_Three_Seconds_Of_Standing_Still_And_Lasts_Six_After_Moving() { }
}
