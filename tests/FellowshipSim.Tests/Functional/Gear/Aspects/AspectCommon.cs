using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using Tar = FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>One hero with one of the weapon abilities it can have, as a theory row.</summary>
public sealed record AspectHeroWeapon(GearHero Hero, int WeaponId)
{
    public string WeaponName => AspectFights.WeaponNames[WeaponId];
    public double WeaponCooldown => AspectFights.WeaponCooldowns[WeaponId];
    public override string ToString() => $"{Hero.Name} with {WeaponName}";
}

/// <summary>What the aspect tests share: loadouts, theory rows over heroes, ranks and weapons, and reading a fight.</summary>
internal static class AspectFights
{
    public const int AmethystSplinters = 31, SapphireAurastone = 32, DiamondStrike = 33, EmeraldJudgement = 34, RubyStorm = 35,
        VisionsOfGrandeur = 47, BraveMachinations = 48, MartialInitiative = 51;

    /// <summary>The weapon abilities' names (game IDs: Voidbringer's Touch 155, Sahril's Wrath 157, Fated Strike 160, Nature's Fury 161, Chronoshift 1558, Icicles of An'zhyr 1561).</summary>
    public static readonly IReadOnlyDictionary<int, string> WeaponNames = new Dictionary<int, string>
    {
        [155] = "Voidbringer's Touch", [157] = "Sahril's Wrath", [160] = "Fated Strike",
        [161] = "Nature's Fury", [1558] = "Chronoshift", [1561] = "Icicles of An'zhyr",
    };

    /// <summary>The weapon abilities' default cooldowns in seconds (tooltips).</summary>
    public static readonly IReadOnlyDictionary<int, double> WeaponCooldowns = new Dictionary<int, double>
    {
        [155] = 90, [157] = 120, [160] = 60, [161] = 60, [1558] = 180, [1561] = 40,
    };

    public static GearLoadout With(int aspectId, int rank = 1, int weapon = 0, double crit = 0, double haste = 0, int enemies = 1,
        double expertise = 0, double spirit = 0, (GemColor, double)[]? gems = null) => new()
    {
        Augmentations = aspectId == 0 ? [] : [(aspectId, rank)],
        WeaponAbilityId = weapon,
        Crit = crit,
        Haste = haste,
        Enemies = enemies,
        Expertise = expertise,
        Spirit = spirit,
        Gems = gems ?? [],
    };

    public static IEnumerable<object[]> HeroRanks =>
        from hero in GearFights.Heroes from rank in new[] { 1, 2, 3, 4 } select new object[] { hero, rank };

    public static IEnumerable<object[]> HeroWeapons =>
        from hero in GearFights.Heroes from weapon in hero.WeaponAbilities select new object[] { new AspectHeroWeapon(hero, weapon) };

    /// <summary>Hero, weapon and rank for the weapons whose crit chance can be read: Voidbringer's Touch erupts at 100% crit chance, which the cap hides.</summary>
    public static IEnumerable<object[]> HeroWeaponRanksWithoutVoidbringers =>
        from hero in GearFights.Heroes from weapon in hero.WeaponAbilities where weapon != VoidbringersTouch from rank in new[] { 1, 2, 3, 4 }
        select new object[] { new AspectHeroWeapon(hero, weapon), rank };

    public static IEnumerable<object[]> HeroWeaponRanks =>
        from hero in GearFights.Heroes from weapon in hero.WeaponAbilities from rank in new[] { 1, 2, 3, 4 }
        select new object[] { new AspectHeroWeapon(hero, weapon), rank };

    /// <summary>Damage events of the ability or effect called <paramref name="name"/>, in order.</summary>
    public static List<StepEvent> AspectHits(this StepDriver f, string name) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.Name == name).ToList();

    /// <summary>The damage events of effect <paramref name="effectId"/> (a spike, a beam, a pulse), in order.</summary>
    public static List<StepEvent> AspectEffectHits(this StepDriver f, int effectId) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId == LogIds.Effect(effectId)).ToList();

    /// <summary>The hero's weapon ability.</summary>
    public static Ability AspectWeapon(this StepDriver f) => f.Hero.WeaponAbility ?? throw new InvalidOperationException("No weapon");

    /// <summary>Seconds left on the weapon ability's cooldown.</summary>
    public static double AspectWeaponCooldownLeft(this StepDriver f) => f.Hero.GetCooldownRemains(f.AspectWeapon().Name, f.Now);

    public const int VoidbringersTouch = 155, NaturesFury = 161, Chronoshift = 1558, IciclesOfAnzhyr = 1561;

    /// <summary>
    /// Casts the weapon ability and lets everything it does happen: 5 seconds for the ones that hit at once or in waves. Voidbringer's Touch only
    /// stores a tenth of the damage dealt and erupts after 15 seconds, so the hero hits three times with its unit hit first and the fight runs to the 16th second.
    /// </summary>
    public static void AspectPlayWeapon(this StepDriver f, GearHero hero)
    {
        var weapon = f.AspectWeapon();
        GearFights.Fill(f);
        f.Cast(weapon);
        if (weapon.Id == VoidbringersTouch)
        {
            for (int i = 0; i < 3; i++) CastWhenReady(f, hero, hero.Unit);
            f.Wait(Math.Max(0.0, 16.0 - f.Now.TotalSeconds));
        }
        else f.Wait(5);
    }

    /// <summary>The weapon's first damage event.</summary>
    public static StepEvent AspectWeaponHit(this StepDriver f) => f.AspectHits(f.AspectWeapon().Name).First();

    /// <summary>Casts the weapon ability, fully resourced, and lets its hits land (waits <paramref name="settle"/> seconds more).</summary>
    public static void AspectCastWeapon(this StepDriver f, double settle = 0)
    {
        GearFights.Fill(f);
        f.Cast(f.AspectWeapon());
        if (settle > 0) f.Wait(settle);
    }

    /// <summary>Time the aura appeared and went, for an aura named <paramref name="name"/> (first gain, first removal after it).</summary>
    public static (double Gained, double Removed) AspectAuraWindow(this StepDriver f, string name)
    {
        var gained = f.Events.First(e => e.Kind == StepEventKind.AuraGained && e.Name == name);
        var removed = f.Events.First(e => e.Kind == StepEventKind.AuraRemoved && e.Name == name && e.Time >= gained.Time);
        return (gained.Time.TotalSeconds, removed.Time.TotalSeconds);
    }

    /// <summary>
    /// The class balancing damage aura of the current patch that multiplies a hero's ability damage (engine constants, <c>Hero.BalancingDamageAura</c>):
    /// Rime 1.04, Ardeos 1.10, Elarion 1.08, Mara 1.04, Gunde and Tariq none. It is on every ability hit, aspect hits included; Ruby Storm's
    /// (a share of health) has none. TODO: check in game that aspect damage gets the class aura.
    /// </summary>
    public static double ClassDamageAura(GearHero hero) => hero.Name switch
    {
        "Rime" => 1.04, "Ardeos" => 1.10, "Elarion" => 1.08, "Mara" => 1.04, _ => 1.0,
    };

    /// <summary>
    /// The ability whose hits crit: the hero's unit hit, except Tariq's, since Wild Swing cannot crit (its damage grows with crit chance instead),
    /// so Face Breaker stands in for it.
    /// </summary>
    public static Type CritUnit(GearHero hero) => hero.Name == "Tariq" ? typeof(Tar.FaceBreaker) : hero.Unit;

    /// <summary>Casts <paramref name="type"/> as soon as the hero can (waiting out cooldowns and global cooldown), resources full.</summary>
    public static void CastWhenReady(StepDriver f, GearHero hero, Type type, EnemyTarget? target = null)
    {
        var ability = hero.Get(f, type);
        for (int i = 0; i < 2000; i++)
        {
            GearFights.Fill(f);
            if (f.TryCast(ability, target).Succeeded) return;
            f.Wait(0.05);
        }
        throw new InvalidOperationException($"{ability.Name} never became castable");
    }
}
