using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Monarch (glyph 705): "Your Major abilities gain 3% / 5% / 8% / 12% Cooldown Acceleration. The bonus is increased by +0.1% for every 1% Haste
/// you have, up to a maximum of 5%" (ranks 1 to 4).
/// Dev: a cooldown's length is the time the cast took plus the cooldown the engine predicts a second after it; with the glyph and without it at the same
/// haste, so each ability's own length (and its own haste scaling) cancels: without / with = 1 + acceleration, to 1e-3. The probe is each hero's Major ability.
/// </summary>
public class GlyphMonarchTests
{
    private static readonly double[] Acceleration = [0.03, 0.05, 0.08, 0.12];

    public static IEnumerable<object[]> HeroRanksHastes =>
        GearFights.Heroes.SelectMany(h => Enumerable.Range(1, 4).SelectMany(r => new[] { 0.0, 0.2, 0.5, 0.8 }.Select(haste => new object[] { h.Name, r, haste })));

    private static double Length(string hero, StepDriver f, Type type) => CooldownLength(f, Hero(hero).Get(f, type));

    /// <summary>
    /// Player: your Major abilities' cooldowns recover faster by the glyph's acceleration (3% / 5% / 8% / 12%), plus 0.1% for every 1% haste, up to 5%.
    /// Dev: haste 0, 20%, 50% and 80% (0, 2%, 5% and 5% from haste), every hero and rank.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanksHastes))]
    public void A_Major_Ability_Recovers_Faster(string hero, int rank, double haste)
    {
        var h = Hero(hero);
        double without = Length(hero, Fight(hero, haste: haste), h.Major);
        double with = Length(hero, Fight(hero, Monarch, rank, haste: haste), h.Major);
        double expected = 1 + Acceleration[rank - 1] + Math.Min(0.05, 0.001 * haste * 100);
        Assert.True(without > 0, "the Major probe must have a cooldown");
        Assert.Equal(expected, without / with, 1e-3);
    }

    /// <summary>
    /// Player: the haste part stops at 5%: at 150% haste the acceleration is the same as at 50%.
    /// Dev: rank 1 at 150% haste, 1 + 0.03 + 0.05.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Haste_Part_Is_Capped_At_Five_Percent(string hero)
    {
        var h = Hero(hero);
        double without = Length(hero, Fight(hero, haste: 1.5), h.Major);
        double with = Length(hero, Fight(hero, Monarch, 1, haste: 1.5), h.Major);
        Assert.Equal(1.08, without / with, 1e-3);
    }

    /// <summary>
    /// Player: only Major abilities: other abilities' cooldowns are the same with the glyph.
    /// Dev: rank 4, 20% haste, every Basic, Core and Power probe that has a cooldown (each hero has at least one).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void Other_Abilities_Are_Not_Touched(string hero)
    {
        var h = Hero(hero);
        int checkedAbilities = 0;
        foreach (var type in new[] { h.Basic, h.Core, h.Power })
        {
            var plain = Fight(hero, haste: 0.2);
            if (h.Get(plain, type).Cooldown.TotalSeconds <= 0) continue;
            double without = Length(hero, plain, type);
            double with = Length(hero, Fight(hero, Monarch, 4, haste: 0.2), type);
            Assert.Equal(without, with, 1e-3);
            checkedAbilities++;
        }
        Assert.True(checkedAbilities > 0);
    }

    public static IEnumerable<object[]> HeroWeapons =>
        GearFights.Heroes.SelectMany(h => h.WeaponAbilities.Select(w => new object[] { h.Name, w }));

    /// <summary>
    /// Player: the glyph leaves every ability that is not a Major ability alone, long cooldowns included (it once sped up every cooldown in the game).
    /// Dev: each hero with each weapon ability it is authorized to have, rank 4, 20% haste: every ability of the hero that is not a Major ability and has a
    /// cooldown of 10 seconds or more (the weapon ability, and the hero's own Core, Power and Control abilities that have one) lasts as long with the glyph
    /// as without. The weapon ability is always among them, so every hero has a long cooldown tested.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void Long_Cooldowns_Of_Other_Abilities_Are_Not_Touched(string hero, int weaponId)
    {
        StepDriver WithGlyph(int glyph) => Hero(hero).Fight(new GearLoadout { Glyphs = glyph == 0 ? [] : [(glyph, 4)], Haste = 0.2, WeaponAbilityId = weaponId });
        var names = WithGlyph(0).Hero.Abilities
            .Where(a => a.Category != AbilityCategory.Major && a.Cooldown.TotalSeconds >= 10)
            .Select(a => a.Name).ToList();
        Assert.Contains(WithGlyph(0).Hero.WeaponAbility!.Name, names);
        foreach (var name in names)
        {
            var plain = WithGlyph(0);
            double without = CooldownLength(plain, plain.Hero.Abilities.First(a => a.Name == name));
            var with = WithGlyph(Monarch);
            double length = CooldownLength(with, with.Hero.Abilities.First(a => a.Name == name));
            Assert.True(without > 0, $"{name} must have a cooldown");
            Assert.Equal(without, length, 1e-3);
        }
    }
}
