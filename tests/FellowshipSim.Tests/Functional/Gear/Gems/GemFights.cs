using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// What the gem tests share: a bare hero with one gem colour at a given power, and readings of the stats the gem nodes change.
/// Dev: by default the hero is built without a character sheet (no sheet percentages), so a gem node's flat stat is added by the
/// engine; a character sheet already includes every gem's stats (<see cref="Sheet"/>), and with one the engine adds nothing for
/// them. A node is tested by the difference between the gem power that unlocks it and the one just below: the other nodes are
/// the same on both sides.
/// </summary>
public static class GemFights
{
    public const double Tolerance = 1e-9;

    /// <summary>A bare hero (no gear, no sheet) with <paramref name="color"/> at <paramref name="power"/>; power 0 for no gem.</summary>
    public static StepDriver Fight(GearHero hero, GemColor color, double power, int enemies = 1, bool sheet = false, double mainStat = 1000)
    {
        var build = new CharacterBuild { Name = "Test " + hero.Name, Hero = hero.Name, PrimaryStat = mainStat };
        if (sheet)
        {
            build.SheetPercents["haste"] = 0;
            build.SheetPercents["crit"] = 0;
            build.SheetPercents["spirit"] = 0;
            build.SheetPercents["expertise"] = 0;
        }
        if (power > 0) build.GemPower[color] = power;
        return new StepDriver(HeroImporter.Import(build), 1, enemies);
    }

    /// <summary>A bare hero (no sheet) with several gems at once.</summary>
    public static StepDriver FightWith(GearHero hero, params (GemColor Color, double Power)[] gems)
    {
        var build = new CharacterBuild { Name = "Test " + hero.Name, Hero = hero.Name, PrimaryStat = 1000 };
        foreach (var (color, power) in gems) build.GemPower[color] = power;
        return new StepDriver(HeroImporter.Import(build), 1, 1);
    }

    /// <summary>What <paramref name="read"/> gives at gem power <paramref name="to"/> less what it gives at <paramref name="from"/>.</summary>
    public static double Delta(GearHero hero, GemColor color, double from, double to, Func<StepDriver, double> read) =>
        read(Fight(hero, color, to)) - read(Fight(hero, color, from));

    /// <summary>What <paramref name="read"/> gives at gem power <paramref name="to"/> over what it gives at <paramref name="from"/>.</summary>
    public static double Ratio(GearHero hero, GemColor color, double from, double to, Func<StepDriver, double> read) =>
        read(Fight(hero, color, to)) / read(Fight(hero, color, from));

    /// <summary>The hero's Major ability cooldown in seconds, from the cast's end to its ready time (it is cast with every resource full).</summary>
    public static double MajorCooldown(this StepDriver f, GearHero hero)
    {
        var major = hero.Get(f, hero.Major);
        hero.CastProbe(f, hero.Major);
        var done = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted && e.Name == major.Name).Time;
        return (f.Now - done).TotalSeconds + f.Ready(major);
    }

    public static readonly string[] StatNames = ["Main Stat", "haste", "crit chance", "crit chance against a full-health enemy", "crit power", "Expertise", "Spirit", "maximum Spirit Points", "Spirit ability cost", "cooldown reduction"];

    /// <summary>The stats a gem node changes, in one list, so two heroes can be compared whole.</summary>
    public static double[] StatVector(this StepDriver f) =>
    [
        f.Main(), f.Haste(), f.Crit(), f.Hero.CritChanceAgainst(f.Enemy, f.Now), f.CritPower(), f.ExpertiseNow(), f.SpiritNow(),
        f.Hero.Resources.Get(Hero.SpiritPoints).Max, f.Hero.SpiritAbilityCost, f.Hero.Stats.CooldownReductionPct,
    ];

    public static double Main(this StepDriver f) => f.Hero.PrimaryAttribute(f.Now);
    public static double Haste(this StepDriver f) => f.Hero.GetHaste(f.Now);
    public static double Crit(this StepDriver f) => f.Hero.CritChance(f.Now);
    public static double CritPower(this StepDriver f) => f.Hero.CritMultiplier(f.Now);
    public static double ExpertiseNow(this StepDriver f) => f.Hero.Expertise(f.Now);
    public static double SpiritNow(this StepDriver f) => f.Hero.SpiritPercent(f.Now);

    /// <summary>The gem of this fight's hero.</summary>
    public static Gem TheGem(this StepDriver f) => f.Hero.Gems.Single();

    /// <summary>The names of the gem's active nodes, in slot order.</summary>
    public static string[] ActiveNodes(this StepDriver f) => f.TheGem().Nodes.Select(n => n.Name).ToArray();

    /// <summary>The first damage of the hero's unit hit with every resource full: the base the damage ratios are read in.</summary>
    public static double UnitHit(GearHero hero, GemColor color, double power)
    {
        var f = Fight(hero, color, power);
        hero.CastProbe(f, hero.Unit);
        return f.Events.First(e => e.Kind == StepEventKind.Damage && e.Value > 0).Value;
    }

    /// <summary>Casts the hero's Spirit ability with full Spirit Points, so Spirit of Heroism starts.</summary>
    public static void StartHeroism(this StepDriver f, GearHero hero) => hero.CastProbe(f, hero.Spirit);

    /// <summary>Ends an enemy as defeated (a kill).</summary>
    public static void Defeat(this StepDriver f, EnemyTarget enemy) => f.Context.RemoveEnemy(enemy, defeated: true);

    public static IEnumerable<StepEvent> Auras(this StepDriver f, StepEventKind kind, string name) =>
        f.Events.Where(e => e.Kind == kind && e.Name == name);

    public static double Ready(this StepDriver f, Ability ability) => f.Hero.GetCooldownRemains(ability.Name, f.Now);

    public static IEnumerable<object[]> Heroes => GearHero.All;
}
