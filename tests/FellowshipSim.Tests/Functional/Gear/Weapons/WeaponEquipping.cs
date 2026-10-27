using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Getting a weapon ability: each hero has the weapon abilities it is authorized to have, and gear that boosts a category of abilities leaves them alone.
/// Dev: the weapon is bound by its weapon ability ID; the hero builds come from <see cref="GearFights"/>. Which weapons a hero is authorized to have is
/// <see cref="GearHero.WeaponAbilities"/>, a list per hero with no grouping of heroes: tests never ask what kind of hero it is.
/// </summary>
public class WeaponEquipping
{
    /// <summary>
    /// Player: with a weapon, the hero has its weapon ability in its abilities and as its weapon ability, in the Weapon category.
    /// Dev: every hero with every weapon it is authorized to have.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Pairs), MemberType = typeof(WeaponHeroes))]
    public void A_Weapon_Gives_The_Hero_Its_Weapon_Ability(GearHero hero, int weaponId, string name)
    {
        var f = hero.WithWeapon(weaponId);

        Assert.NotNull(f.Hero.WeaponAbility);
        Assert.Equal(name, f.Hero.WeaponAbility!.Name);
        Assert.Equal(weaponId, f.Hero.WeaponAbility.Id);
        Assert.Equal(AbilityCategory.Weapon, f.Hero.WeaponAbility.Category);
        Assert.Contains(f.Hero.Abilities, a => a == f.Hero.WeaponAbility);
        Assert.NotNull(f.Hero.EquippedWeapon);
    }

    /// <summary>Player: a hero with no weapon has no weapon ability. Dev: the bare hero builds.</summary>
    [Theory]
    [MemberData(nameof(GearHero.All), MemberType = typeof(GearHero))]
    public void A_Hero_Without_A_Weapon_Has_No_Weapon_Ability(GearHero hero)
    {
        var f = hero.Fight();

        Assert.Null(f.Hero.WeaponAbility);
        Assert.DoesNotContain(f.Hero.Abilities, a => a.Category == AbilityCategory.Weapon);
    }

    /// <summary>
    /// Player: a weapon ability can be used in a fight (it is cast, goes on cooldown, and refuses a second cast while on cooldown).
    /// Dev: every hero with every weapon it is authorized to have; a cast, then a second one at once.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Pairs), MemberType = typeof(WeaponHeroes))]
    public void A_Weapon_Ability_Goes_On_Cooldown_When_Cast(GearHero hero, int weaponId, string name)
    {
        var f = hero.WithWeapon(weaponId);
        var weapon = f.Weapon();

        f.Cast(weapon);

        Assert.False(f.TryCast(weapon).Succeeded);
        Assert.Contains(f.Events, e => e.Kind == StepEventKind.CastCompleted && e.Name == name);
    }

    /// <summary>
    /// Player: Earthbreaker is one of the weapon abilities Gunde, Mara and Tariq can have.
    /// Dev: placeholder, no test can run: the engine has no Earthbreaker (no ability, no weapon, so a build asking for one has it unmodeled), so it is in
    /// no hero's authorized list in <see cref="GearHero.WeaponAbilities"/>.
    /// </summary>
    [Fact(Skip = "Missing feature: Earthbreaker is not in the engine. TODO: add it (ability, weapon, ID), add it to Gunde's, Mara's and Tariq's lists, and write its tests (damage, cooldown, cast time, what it does) from its tooltip.")]
    public void Earthbreaker_Is_A_Weapon_Ability_Of_Gunde_Mara_And_Tariq()
    {
        Assert.Fail("Earthbreaker is not modeled");
    }

    // ---- Category-keyed gear leaves weapon abilities alone

    /// <summary>
    /// Player: The Sinister (Core abilities' crit chance and crit power) does not touch a weapon ability; The Mystic (Power abilities' damage per Spirit)
    /// neither; The Trickster (Power abilities grant crit chance) is not set off by it.
    /// Dev: each hero with the first of its authorized weapons that hits at once (Fated Strike or Chronoshift), glyphs at rank 4, 30% crit and 30% Spirit: the weapon's
    /// first hit has the same damage and crit chance with and without the glyph, and The Trickster's buff (effect 3181) never appears.
    /// </summary>
    [Theory]
    [MemberData(nameof(GearHero.All), MemberType = typeof(GearHero))]
    public void Glyphs_Of_Other_Categories_Do_Not_Change_A_Weapon_Ability(GearHero hero)
    {
        int weaponId = hero.WeaponAbilities.First(w => w is WeaponHeroes.FatedStrikeId or WeaponHeroes.ChronoshiftId);
        string name = WeaponHeroes.NameOf(weaponId);

        StepEvent First(params (int Id, int Rank)[] glyphs)
        {
            var f = hero.WithWeapon(weaponId, crit: 0.3, gear: new GearLoadout { Glyphs = glyphs, Spirit = 0.3 });
            f.Cast(f.Weapon());
            Assert.DoesNotContain(f.Events, e => e.Kind == StepEventKind.AuraGained && e.Name == "The Trickster");
            return f.Damage(name).First();
        }

        var plain = First();
        foreach (int glyph in new[] { 701, 706, 700 })
        {
            var with = First((glyph, 4));
            Assert.Equal(plain.Value, with.Value, 1e-6);
            Assert.Equal(plain.CritChance, with.CritChance, 1e-9);
        }
    }

    /// <summary>
    /// Player: a weapon ability ignores cooldown recovery: the cooldown speed-ups that other abilities get (here The Monarch's acceleration, and haste) do not
    /// make it come back sooner.
    /// Dev: each hero with an authorized weapon with no recovery effect of its own (Fated Strike or Icicles of An'zhyr), 50% haste and The Monarch at rank 4:
    /// 10 seconds after the cast, exactly 10 seconds are off the cooldown (60 and 40 seconds).
    /// </summary>
    [Theory]
    [MemberData(nameof(GearHero.All), MemberType = typeof(GearHero))]
    public void Cooldown_Recovery_Does_Not_Speed_Up_A_Weapon_Ability(GearHero hero)
    {
        int weaponId = hero.WeaponAbilities.First(w => w is WeaponHeroes.FatedStrikeId or WeaponHeroes.IciclesId);
        double cooldown = GearAspect.AspectFights.WeaponCooldowns[weaponId];
        var f = hero.WithWeapon(weaponId, haste: 0.5, gear: new GearLoadout { Glyphs = [(705, 4)] });
        f.Cast(f.Weapon());
        double castAt = f.CompletedAt(WeaponHeroes.NameOf(weaponId));
        f.WaitUntil(castAt + 10);

        Assert.Equal(cooldown - 10, f.CooldownSecondsLeft(f.Weapon()), 0.01);
    }
}
