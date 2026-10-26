using FellowshipSim.Core.Customization.CommonAbilities;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearRelic;

/// <summary>
/// The relic passives, each on every hero: Accursed Trophy, Dragonbone Dice, Undying Heart and Gilded Broach, two relics at once, and that protective
/// enchants and the common abilities leave damage alone.
/// Dev: a relic works through the hero's categories and events, not through named abilities, so every test is a theory over <see cref="GearHero.All"/>
/// with the hero's probe abilities. Buffs are read as the hero's Main Stat or haste (ratios, so no hero's numbers matter); procs are forced and their
/// chance read from <c>Rolls</c>. Crit 100% makes every hit a crit (the expected-value mode counts a hit as a crit above 50%).
/// </summary>
public class RelicPassives
{
    private const double Ratio = 0.001;

    public static IEnumerable<object[]> Heroes => GearHero.All;

    // ---- Accursed Trophy

    /// <summary>
    /// Player: Accursed Trophy: your Spirit ability grants Drakheim's Absolution, +20% Primary Stat for 20 seconds.
    /// Dev: the Spirit probe cast on a hero with and without the relic; Main Stat read when the cast is over, then again 19.9s and 20.1s after the buff was gained.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Accursed_Trophy_Gives_20_Percent_Main_Stat_For_20_Seconds_After_The_Spirit_Ability(GearHero hero)
    {
        var plain = hero.Fight();
        hero.CastProbe(plain, hero.Spirit);
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfInnerSeas));
        hero.CastProbe(f, hero.Spirit);

        Assert.True(f.Has(AccursedTrophy.Buff));
        Assert.Equal(plain.MainStat() * 1.2, f.MainStat(), 3);

        double gained = f.GainedAt("Drakheim's Absolution");
        f.Wait(gained + 19.9 - f.Now.TotalSeconds);
        Assert.True(f.Has(AccursedTrophy.Buff));
        f.Wait(0.2);
        Assert.False(f.Has(AccursedTrophy.Buff));
    }

    /// <summary>
    /// Player: Accursed Trophy's damage bonus: hits after the Spirit ability deal 20% more than they would without the relic.
    /// Dev: the unit hit after the Spirit probe, with the relic over without it (the Spirit ability's own effects are in both).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Accursed_Trophy_Raises_Damage_By_20_Percent_After_The_Spirit_Ability(GearHero hero)
    {
        double Hit(GearLoadout loadout)
        {
            var f = hero.Fight(loadout);
            hero.CastProbe(f, hero.Spirit);
            int before = f.Events.Count;
            hero.CastProbe(f, hero.Unit);
            return f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Value > 0).Value;
        }
        Assert.Equal(1.2, Hit(RelicFights.With(RelicFights.OrbOfInnerSeas)) / Hit(new GearLoadout()), 3);
    }

    /// <summary>
    /// Player: only the Spirit ability grants Drakheim's Absolution, no other ability does.
    /// Dev: one probe of every other category, nothing cast earlier.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Accursed_Trophy_Is_Not_Granted_By_Other_Abilities(GearHero hero)
    {
        foreach (var type in new[] { hero.Basic, hero.Core, hero.Power, hero.Major })
        {
            var f = hero.Fight(RelicFights.With(RelicFights.OrbOfInnerSeas));
            hero.CastProbe(f, type);
            Assert.False(f.Has(AccursedTrophy.Buff), $"{type.Name} granted it");
        }
    }

    /// <summary>Player: without the relic the Spirit ability grants no Drakheim's Absolution. Dev: the Spirit probe on a bare hero.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Spirit_Ability_Grants_No_Absolution_Without_The_Relic(GearHero hero)
    {
        var f = hero.Fight();
        hero.CastProbe(f, hero.Spirit);
        Assert.False(f.Has(AccursedTrophy.Buff));
    }

    // ---- Dragonbone Dice

    /// <summary>
    /// Player: Dragonbone Dice: your critical strikes have a chance (0.9 PPM) to give Draconic Might, +18% Primary Stat for 14 seconds.
    /// Dev: crit 100%, the proc forced; one cast of an ability whose hits crit (the unit ability; Tariq's Heavy Strike, since Wild Swing cannot crit). Main Stat read after the hit, then 13.9s and 14.1s after the buff was gained.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Dragonbone_Dice_Gives_18_Percent_Main_Stat_For_14_Seconds_On_A_Crit(GearHero hero)
    {
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfAwakenedGaze, crit: 1.0));
        f.ForceProcs(key => key == "dragonbone_dice");
        double before = f.MainStat();
        hero.CastProbe(f, RelicFights.CritAbility(hero));

        Assert.True(f.Has(DragonboneDice.Buff));
        Assert.Equal(before * 1.18, f.MainStat(), 3);

        f.Wait(f.GainedAt("Draconic Might") + 13.9 - f.Now.TotalSeconds);
        Assert.True(f.Has(DragonboneDice.Buff));
        f.Wait(0.2);
        Assert.False(f.Has(DragonboneDice.Buff));
    }

    /// <summary>
    /// Player: Dragonbone Dice only triggers from critical strikes: a hero with no crit chance never rolls it.
    /// Dev: crit 0%, one hit of the unit ability; no roll with the proc's key.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Dragonbone_Dice_Does_Not_Roll_Without_A_Crit(GearHero hero)
    {
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfAwakenedGaze));
        var procs = f.ForceProcs(_ => true);
        hero.CastProbe(f, RelicFights.CritAbility(hero));
        Assert.DoesNotContain(procs.Rolls, r => r.Key == "dragonbone_dice");
        Assert.False(f.Has(DragonboneDice.Buff));
    }

    /// <summary>
    /// Player: a failed Dragonbone Dice roll gives nothing.
    /// Dev: crit 100%, every proc forced to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Dragonbone_Dice_Gives_Nothing_When_The_Roll_Fails(GearHero hero)
    {
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfAwakenedGaze, crit: 1.0));
        var procs = f.ForceProcs(_ => false);
        hero.CastProbe(f, RelicFights.CritAbility(hero));
        Assert.Contains(procs.Rolls, r => r.Key == "dragonbone_dice");
        Assert.False(f.Has(DragonboneDice.Buff));
    }

    /// <summary>
    /// Player: Dragonbone Dice is a real-PPM proc: 0.9 per minute, scaled by haste, so the chance of a roll grows with the time since the last one.
    /// Dev: the first roll comes at the first crit; its chance is read from Rolls: 0.9 x (1 + haste) x seconds / 60, with 0 and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Dragonbone_Dice_Chance_Is_0_9_Procs_Per_Minute_Scaled_By_Haste(double haste)
    {
        var hero = GearFights.Heroes.First(h => h.Name == "Gunde");
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfAwakenedGaze, haste, crit: 1.0));
        var procs = f.ForceProcs(_ => false);
        f.Wait(10.0);
        hero.CastProbe(f, RelicFights.CritAbility(hero));
        var roll = procs.Rolls.First(r => r.Key == "dragonbone_dice");
        double at = f.Events.First(e => e.Kind == StepEventKind.Damage).Time.TotalSeconds;
        Assert.Equal(0.9 * (1.0 + haste) * at / 60.0, roll.Chance, 3);
    }

    // ---- Undying Heart

    /// <summary>
    /// Player: Undying Heart: your abilities have a chance (0.8 PPM) to give Dark Prophecy, +25% Haste for 20 seconds.
    /// Dev: the proc forced; one cast of the unit ability. Haste read after the cast (0 before), then 19.9s and 20.1s after the buff was gained.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Undying_Heart_Gives_25_Percent_Haste_For_20_Seconds(GearHero hero)
    {
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfGatheringStorms));
        f.ForceProcs(key => key == "undying_heart");
        Assert.Equal(0.0, f.Hero.GetHaste(f.Now), 3);
        hero.CastProbe(f, hero.Unit);

        Assert.True(f.Has(UndyingHeart.Buff));
        Assert.Equal(0.25, f.Hero.GetHaste(f.Now), 3);

        f.Wait(f.GainedAt("Dark Prophecy") + 19.9 - f.Now.TotalSeconds);
        Assert.True(f.Has(UndyingHeart.Buff));
        f.Wait(0.2);
        Assert.False(f.Has(UndyingHeart.Buff));
    }

    /// <summary>
    /// Player: Undying Heart rolls on every ability you use, whatever its category.
    /// Dev: one probe of each category on a fresh hero, the proc forced to fail; each cast makes exactly one roll.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Undying_Heart_Rolls_Once_For_Each_Ability_Of_Every_Category(GearHero hero)
    {
        foreach (var type in new[] { hero.Basic, hero.Core, hero.Power, hero.Major, hero.Spirit })
        {
            var f = hero.Fight(RelicFights.With(RelicFights.OrbOfGatheringStorms));
            var procs = f.ForceProcs(_ => false);
            hero.CastProbe(f, type);
            Assert.Equal(1, procs.Rolls.Count(r => r.Key == "undying_heart"));
        }
    }

    /// <summary>
    /// Player: Undying Heart's chance is 0.8 procs per minute scaled by haste.
    /// Dev: the first roll comes at the end of the first cast; its chance is read from Rolls, with 0 and 50% haste, after a 10s wait.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Undying_Heart_Chance_Is_0_8_Procs_Per_Minute_Scaled_By_Haste(double haste)
    {
        var hero = GearFights.Heroes.First(h => h.Name == "Gunde");
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfGatheringStorms, haste));
        var procs = f.ForceProcs(_ => false);
        f.Wait(10.0);
        double start = f.Now.TotalSeconds;
        hero.CastProbe(f, hero.Unit);
        var roll = procs.Rolls.First(r => r.Key == "undying_heart");
        double completed = f.Events.First(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        Assert.Equal(0.8 * (1.0 + haste) * completed / 60.0, roll.Chance, 3);
        Assert.True(completed >= start);
    }

    // ---- Gilded Broach

    /// <summary>
    /// Player: Gilded Broach gives +5% Main Stat as a passive (on top of its healing when you drop below 20% health).
    /// Dev: Main Stat read on a hero with the relic over a bare hero. FAILS ON PURPOSE: the engine has the relic as a defensive-only passive and gives no Main Stat.
    /// TODO: missing feature (user says +5% Main Stat). Assumed to multiply Main Stat like the other percentage Main Stat buffs; if the game adds it to another
    /// percentage bonus instead, the number below is the same alone but changes when others are present.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Gilded_Broach_Gives_5_Percent_Main_Stat(GearHero hero)
    {
        var plain = hero.Fight();
        var f = hero.Fight(RelicFights.With(RelicFights.OrbOfSanguineRestoration));
        Assert.Equal(plain.MainStat() * 1.05, f.MainStat(), 3);
    }

    /// <summary>
    /// Player: Gilded Broach's Main Stat is a big damage boost: hits deal 5% more.
    /// Dev: the unit hit over a bare hero's. FAILS ON PURPOSE with the test above (missing feature).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Gilded_Broach_Raises_Damage_By_5_Percent(GearHero hero)
    {
        double with = RelicFights.UnitHit(hero, RelicFights.With(RelicFights.OrbOfSanguineRestoration));
        double without = RelicFights.UnitHit(hero, new GearLoadout());
        Assert.Equal(1.05, with / without, 3);
    }

    // ---- Two relics

    /// <summary>
    /// Player: two relics work together: Accursed Trophy and Undying Heart both apply.
    /// Dev: both orbs worn, the Spirit probe cast with the Undying Heart proc forced; both buffs are up.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Two_Relics_Both_Apply(GearHero hero)
    {
        var f = hero.Fight(new GearLoadout { Relics = [RelicFights.OrbOfInnerSeas, RelicFights.OrbOfGatheringStorms] });
        f.ForceProcs(key => key == "undying_heart");
        hero.CastProbe(f, hero.Spirit);
        Assert.True(f.Has(AccursedTrophy.Buff));
        Assert.True(f.Has(UndyingHeart.Buff));
    }

    // ---- Protective enchants and common abilities

    public static IEnumerable<object[]> ProtectiveEnchantIds => Core.Catalog.AllAugmentations.OfType<ProtectiveEnchant>().Select(e => new object[] { e.Id });

    /// <summary>
    /// Player: protective enchants are defensive: wearing one changes no damage you deal.
    /// Dev: the unit hit on Rime and Tariq with each enchant over a bare hero's.
    /// </summary>
    [Theory]
    [MemberData(nameof(ProtectiveEnchantIds))]
    public void Protective_Enchants_Change_No_Damage(int enchantId)
    {
        foreach (var hero in GearFights.Heroes.Where(h => h.Name is "Rime" or "Tariq"))
        {
            double with = RelicFights.UnitHit(hero, new GearLoadout { Augmentations = [(enchantId, 1)] });
            Assert.Equal(1.0, with / RelicFights.UnitHit(hero, new GearLoadout()), 6);
        }
    }

    /// <summary>
    /// Player: every hero has the shared abilities (Revive, Major Dispel, Anchor, ...) and casting one deals no damage.
    /// Dev: Anchor (a 0.667s cast, no damage) cast on each hero; it is on the hero's list and no damage event follows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Common_Abilities_Are_On_Every_Hero_And_Deal_No_Damage(GearHero hero)
    {
        var f = hero.Fight();
        Assert.Contains(f.Hero.Abilities, a => a is Anchor);
        Assert.Contains(f.Hero.Abilities, a => a is Revive);
        f.Cast<Anchor>();
        Assert.DoesNotContain(f.Events, e => e.Kind == StepEventKind.Damage);
    }
}
