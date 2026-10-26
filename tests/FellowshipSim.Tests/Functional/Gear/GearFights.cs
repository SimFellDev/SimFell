using FellowshipSim.Core.Customization.Gems;
using WA = FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using Ard = FellowshipSim.Core.Heroes.Ardeos.Abilities;
using Ela = FellowshipSim.Core.Heroes.Elarion.Abilities;
using Gun = FellowshipSim.Core.Heroes.Gunde.Abilities;
using Mar = FellowshipSim.Core.Heroes.Mara.Abilities;
using Rim = FellowshipSim.Core.Heroes.Rime.Abilities;
using Tar = FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>What a bare test hero wears for the gear tests; everything is empty or zero by default.</summary>
public sealed class GearLoadout
{
    public double Haste { get; init; }
    public double Crit { get; init; }
    public double Spirit { get; init; }
    public double Expertise { get; init; }
    public double MainStat { get; init; } = 1000;
    public int Enemies { get; init; } = 1;
    public int Seed { get; init; } = 1;
    /// <summary>Glyphs by game ID and rank.</summary>
    public (int Id, int Rank)[] Glyphs { get; init; } = [];
    /// <summary>Aspects, sigils and protective enchants by game ID and rank.</summary>
    public (int Id, int Rank)[] Augmentations { get; init; } = [];
    /// <summary>The weapon ability's game ID; 0 for no weapon.</summary>
    public int WeaponAbilityId { get; init; }
    /// <summary>Relic item IDs (up to two).</summary>
    public int[] Relics { get; init; } = [];
    /// <summary>Gem power per colour, as a character sheet shows it.</summary>
    public (GemColor Color, double Power)[] Gems { get; init; } = [];
}

/// <summary>
/// One hero as the gear tests see it: how to build it with a loadout and one ability of each category to probe an effect with.
/// A gear effect hooks the hero through the ability's category, so a probe is "the hero's Core ability" and not a named ability.
/// </summary>
public sealed record GearHero(string Name, MainStat MainStat, Type Basic, Type Core, Type Power, Type Major, Type Spirit, Type? Control, Type Unit)
{
    /// <summary>The weapon abilities the hero can have (game IDs): the melee set or the ranged set.</summary>
    public int[] WeaponAbilities => IsMelee
        ? [WA.FatedStrike.AbilityId, WA.SahrilsWrath.AbilityId, WA.VoidbringersTouch.AbilityId]
        : [WA.Chronoshift.AbilityId, WA.IciclesOfAnzhyr.AbilityId, WA.VoidbringersTouch.AbilityId, WA.NaturesFury.AbilityId];

    public bool IsMelee => MainStat == MainStat.Strength || Name == "Mara";

    public override string ToString() => Name;

    public StepDriver Fight(GearLoadout? loadout = null)
    {
        loadout ??= new GearLoadout();
        var build = new CharacterBuild { Name = "Test " + Name, Hero = Name, PrimaryStat = loadout.MainStat, WeaponAbilityId = loadout.WeaponAbilityId };
        build.SheetPercents["haste"] = loadout.Haste;
        build.SheetPercents["crit"] = loadout.Crit;
        build.SheetPercents["spirit"] = loadout.Spirit;
        build.SheetPercents["expertise"] = loadout.Expertise;
        foreach (var (id, rank) in loadout.Glyphs) build.Glyphs.Add(new BuildEntry(id, rank));
        foreach (var (id, rank) in loadout.Augmentations) build.Augmentations.Add(new BuildEntry(id, rank));
        foreach (var (color, power) in loadout.Gems) build.GemPower[color] = power;
        if (loadout.Relics.Length > 0) build.Relic1ItemId = loadout.Relics[0];
        if (loadout.Relics.Length > 1) build.Relic2ItemId = loadout.Relics[1];
        return new StepDriver(HeroImporter.Import(build), loadout.Seed, loadout.Enemies);
    }

    /// <summary>The hero's ability of <paramref name="type"/>.</summary>
    public Ability Get(StepDriver f, Type type) => f.Hero.Abilities.First(a => a.GetType() == type);

    /// <summary>Casts the hero's ability of that type, having filled every resource (so costs never refuse the cast).</summary>
    public void CastProbe(StepDriver f, Type type)
    {
        GearFights.Fill(f);
        f.Cast(Get(f, type));
    }

    public static IEnumerable<object[]> All => GearFights.Heroes.Select(h => new object[] { h });
}

/// <summary>The heroes the gear tests run over, and what the gear tests share.</summary>
public static class GearFights
{
    public static IReadOnlyList<GearHero> Heroes { get; } =
    [
        new("Rime", MainStat.Intellect, typeof(Rim.FrostBolt), typeof(Rim.BurstingIce), typeof(Rim.IceComet), typeof(Rim.IceBlitz), typeof(Rim.WrathOfWinter), null, typeof(Rim.FrostBolt)),
        new("Ardeos", MainStat.Intellect, typeof(Ard.InfernalWave), typeof(Ard.FireBall), typeof(Ard.Detonate), typeof(Ard.Apocalypse), typeof(Ard.Incinerate), null, typeof(Ard.FireBall)),
        new("Elarion", MainStat.Agility, typeof(Ela.FocusedShot), typeof(Ela.Multishot), typeof(Ela.HighwindArrow), typeof(Ela.LunarlightMark), typeof(Ela.EventHorizon), null, typeof(Ela.FocusedShot)),
        new("Gunde", MainStat.Strength, typeof(Gun.DoubleStrike), typeof(Gun.BloodArc), typeof(Gun.Rupture), typeof(Gun.Slaughter), typeof(Gun.BloodboundSpirit), typeof(Gun.ButchersHook), typeof(Gun.DoubleStrike)),
        new("Mara", MainStat.Agility, typeof(Mar.SkitteringBlades), typeof(Mar.WidowsBite), typeof(Mar.QueensFang), typeof(Mar.MaidenOfDeath), typeof(Mar.MatriarchMacabre), null, typeof(Mar.SkitteringBlades)),
        new("Tariq", MainStat.Strength, typeof(Tar.WildSwing), typeof(Tar.HeavyStrike), typeof(Tar.SkullCrusher), typeof(Tar.ThunderCall), typeof(Tar.RagingTempest), null, typeof(Tar.WildSwing)),
    ];

    /// <summary>Every resource of the hero full (Spirit Points too), so any ability can be cast.</summary>
    public static void Fill(StepDriver f)
    {
        foreach (var pool in f.Hero.Resources.States) pool.Current = pool.Max;
    }
}
