using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Inspired Allegiance (sigil 2): "Your abilities have a chance to reduce the Cooldown of your Weapon Ability by 1.5 / 4 / 6 / 8 seconds and grant you
/// and 1 / 2 / 3 / 3 allies 6 / 10 / 13 / 17 Haste rating for 8 seconds. (1.2 PPM)"
/// Dev: the proc is forced. The haste is read off the hero (a rating is 0.16%); the cooldown part runs over every hero and every weapon the hero can
/// have, comparing two identical fights where the proc succeeds in one and fails in the other. The allies' share is not simulated.
/// </summary>
public class SigilInspiredAllegiance
{
    private const int Sigil = 2;
    private const double ExpiryLag = 0.05;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(6 * 0.0016, 10 * 0.0016, 13 * 0.0016, 17 * 0.0016);

    public static IEnumerable<object[]> HeroesAndWeapons =>
        GearFights.Heroes.SelectMany(h => h.WeaponAbilities.Select(w => new object[] { h, w }));

    public static IEnumerable<object[]> WeaponsByRank =>
        GearFights.Heroes.SelectMany(h => h.WeaponAbilities.SelectMany(w =>
            new[] { 1.5, 4.0, 6.0, 8.0 }.Select((s, i) => new object[] { h, w, i + 1, s })));

    /// <summary>
    /// Player: the proc gives 6 / 10 / 13 / 17 Haste rating by rank.
    /// Dev: haste after a Basic ability with the proc forced is rating x 0.16%.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void The_Proc_Grants_Haste_Rating_By_Rank(GearHero hero, int rank, double haste)
    {
        var f = SigilFights.Fight(hero, Sigil, rank);
        f.Force("inspired_allegiance");
        f.CastBasic(hero);
        Assert.Equal(haste, f.Haste(), 1e-6);
    }

    /// <summary>
    /// Player: the haste lasts 8 seconds.
    /// Dev: one forced proc; the buff is removed 8s after it was gained, within a server frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Haste_Lasts_Eight_Seconds(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Force("inspired_allegiance", 1);
        f.CastBasic(hero);
        f.Wait(9);
        Assert.Equal(8.0, f.RemovedAt("Inspired Allegiance")[0] - f.GainedAt("Inspired Allegiance")[0], ExpiryLag);
    }

    /// <summary>
    /// Player: without the proc nothing happens.
    /// Dev: a Basic ability with the proc failing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_The_Proc_There_Is_No_Haste(GearHero hero)
    {
        var f = SigilFights.Fight(hero, Sigil, 1);
        f.Force("inspired_allegiance", 0);
        f.CastBasic(hero);
        Assert.Equal(0.0, f.Haste(), 1e-9);
    }

    /// <summary>
    /// Player: the chance is 1.2 procs per minute, more with haste, rolled by every ability used.
    /// Dev: a cast after 5s without any: the chance given is 1.2 x (1 + haste) x (the cast's completion time) / 60.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Chance_Is_1_2_Procs_Per_Minute(double haste)
    {
        var hero = GearFights.Heroes[0];
        var f = SigilFights.Fight(hero, Sigil, 1, haste: haste);
        var procs = f.Force("inspired_allegiance", 0);
        f.Wait(5);
        f.CastBasic(hero);
        var chance = procs.Rolls.First(r => r.Key == "inspired_allegiance").Chance;
        Assert.Equal(1.2 * (1 + haste) * f.LastCastCompletedAt() / 60.0, chance, 0.0005);
    }

    /// <summary>
    /// Player: the proc takes 1.5 / 4 / 6 / 8 seconds off the weapon ability's cooldown, whichever weapon the hero has.
    /// Dev: the weapon ability is cast, then a Basic ability follows; the weapon's cooldown left is compared between a fight where the proc succeeds and one
    /// where it fails. The tolerance is 0.15s: a Voidbringer's Touch cooldown recovers with haste, and the proc's haste adds up to 0.04s over the cast.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponsByRank))]
    public void The_Proc_Shortens_The_Weapon_Cooldown(GearHero hero, int weapon, int rank, double seconds)
    {
        double Remaining(bool proc)
        {
            var f = SigilFights.Fight(hero, Sigil, rank, weapon: weapon);
            bool armed = false;
            f.Force("inspired_allegiance", () => armed && proc);
            GearFights.Fill(f);
            f.Cast(f.Hero.WeaponAbility!);
            armed = true;
            f.CastBasic(hero);
            return f.Hero.GetCooldownRemains(f.Hero.WeaponAbility!.Name, f.Now);
        }

        // The proc must not roll on the weapon cast itself, only on the Basic ability after it.
        Assert.Equal(seconds, Remaining(false) - Remaining(true), 0.15);
    }
}
