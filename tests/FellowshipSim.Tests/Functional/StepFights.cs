using FellowshipSim.Core;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>Builds the heroes for functional tests: no gear and no Spirit (so no Spirit Refund), and the talents and haste a test asks for.</summary>
public static class StepFights
{
    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="talents">Talent IDs, e.g. <c>Burstbolter.TalentId</c>.</param>
    /// <param name="crit">Crit chance as a fraction (1.0 is 100%); 0 by default, so hits are not crit.</param>
    /// <param name="spirit">Spirit as a fraction (0.2 is 20%); 0 by default, so there is no Spirit Refund.</param>
    public static StepDriver Rime(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0)
    {
        var build = new CharacterBuild { Name = "Test Rime", Hero = "Rime", PrimaryStat = 1000 };
        build.SheetPercents["haste"] = haste;
        build.SheetPercents["crit"] = crit;
        build.SheetPercents["spirit"] = spirit;
        if (talents != null) build.Talents.AddRange(talents);
        return new StepDriver(HeroImporter.Import(build), seed, enemies);
    }


    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="talents">Talent IDs, e.g. <c>Fusillade.TalentId</c>.</param>
    /// <param name="crit">Crit chance as a fraction; 0 by default, so hits are not crit.</param>
    /// <param name="spirit">Spirit as a fraction; 0 by default, so there is no Spirit Refund.</param>
    /// <param name="mainStat">Agility.</param>
    /// <param name="expertise">Expertise as a fraction; 0 by default.</param>
    /// <param name="weaponAbilityId">The weapon ability's ID, e.g. <c>Chronoshift.AbilityId</c>; 0 for no weapon.</param>
    /// <param name="legendaries">Item IDs of legendary items worn, e.g. <c>Shimmer.HelmItemId</c>.</param>
    public static StepDriver Elarion(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 1000, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null)
    {
        var build = new CharacterBuild { Name = "Test Elarion", Hero = "Elarion", PrimaryStat = mainStat, WeaponAbilityId = weaponAbilityId };
        build.SheetPercents["expertise"] = expertise;
        build.SheetPercents["haste"] = haste;
        build.SheetPercents["crit"] = crit;
        build.SheetPercents["spirit"] = spirit;
        if (talents != null) build.Talents.AddRange(talents);
        if (legendaries != null) build.LegendaryItemIds.AddRange(legendaries);
        return new StepDriver(HeroImporter.Import(build), seed, enemies);
    }

    public static Core.Heroes.Elarion.Elarion ElarionOf(this StepDriver driver) => (Core.Heroes.Elarion.Elarion)driver.Hero;


    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="talents">Talent IDs, e.g. <c>SlowBurn.TalentId</c>.</param>
    /// <param name="mainStat">Intellect.</param>
    /// <param name="expertise">Expertise as a fraction; 0 by default.</param>
    /// <param name="crit">Crit chance as a fraction; 0 by default, so hits are not crit.</param>
    /// <param name="legendaries">Item IDs of legendary items worn.</param>
    public static StepDriver Ardeos(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 1000, double expertise = 0, int[]? legendaries = null)
    {
        var build = new CharacterBuild { Name = "Test Ardeos", Hero = "Ardeos", PrimaryStat = mainStat };
        build.SheetPercents["haste"] = haste;
        build.SheetPercents["crit"] = crit;
        build.SheetPercents["spirit"] = spirit;
        build.SheetPercents["expertise"] = expertise;
        if (talents != null) build.Talents.AddRange(talents);
        if (legendaries != null) build.LegendaryItemIds.AddRange(legendaries);
        return new StepDriver(HeroImporter.Import(build), seed, enemies);
    }

    public static Core.Heroes.Ardeos.Ardeos ArdeosOf(this StepDriver driver) => (Core.Heroes.Ardeos.Ardeos)driver.Hero;

    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="talents">Talent IDs, e.g. <c>SinnersPride.TalentId</c>.</param>
    /// <param name="crit">Crit chance as a fraction; 0 by default, so hits are not crit.</param>
    /// <param name="mainStat">Agility.</param>
    public static StepDriver Mara(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 1000, double expertise = 0, int[]? legendaries = null)
    {
        var build = new CharacterBuild { Name = "Test Mara", Hero = "Mara", PrimaryStat = mainStat };
        build.SheetPercents["haste"] = haste;
        build.SheetPercents["crit"] = crit;
        build.SheetPercents["spirit"] = spirit;
        build.SheetPercents["expertise"] = expertise;
        if (talents != null) build.Talents.AddRange(talents);
        if (legendaries != null) build.LegendaryItemIds.AddRange(legendaries);
        return new StepDriver(HeroImporter.Import(build), seed, enemies);
    }

    public static Core.Heroes.Mara.Mara MaraOf(this StepDriver driver) => (Core.Heroes.Mara.Mara)driver.Hero;

    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="talents">Talent IDs, e.g. <c>DeepRend.TalentId</c>.</param>
    /// <param name="crit">Crit chance as a fraction; 0 by default, so hits are not crit.</param>
    /// <param name="spirit">Spirit as a fraction; 0 by default, so there is no Spirit Refund.</param>
    /// <param name="mainStat">Strength.</param>
    public static StepDriver Gunde(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 1000, double expertise = 0, int[]? legendaries = null)
    {
        var build = new CharacterBuild { Name = "Test Gunde", Hero = "Gunde", PrimaryStat = mainStat };
        build.SheetPercents["haste"] = haste;
        build.SheetPercents["crit"] = crit;
        build.SheetPercents["spirit"] = spirit;
        build.SheetPercents["expertise"] = expertise;
        if (talents != null) build.Talents.AddRange(talents);
        if (legendaries != null) build.LegendaryItemIds.AddRange(legendaries);
        return new StepDriver(HeroImporter.Import(build), seed, enemies);
    }

    public static Core.Heroes.Gunde.Gunde GundeOf(this StepDriver driver) => (Core.Heroes.Gunde.Gunde)driver.Hero;

    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="talents">Talent IDs, e.g. <c>Bloodline.TalentId</c>.</param>
    /// <param name="crit">Crit chance as a fraction; 0 by default, so hits are not crit.</param>
    /// <param name="spirit">Spirit as a fraction; 0 by default, so there is no Spirit Refund.</param>
    /// <param name="mainStat">Strength.</param>
    public static StepDriver Tariq(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 1000, double expertise = 0, int[]? legendaries = null)
    {
        var build = new CharacterBuild { Name = "Test Tariq", Hero = "Tariq", PrimaryStat = mainStat };
        build.SheetPercents["haste"] = haste;
        build.SheetPercents["crit"] = crit;
        build.SheetPercents["spirit"] = spirit;
        build.SheetPercents["expertise"] = expertise;
        if (talents != null) build.Talents.AddRange(talents);
        if (legendaries != null) build.LegendaryItemIds.AddRange(legendaries);
        return new StepDriver(HeroImporter.Import(build), seed, enemies);
    }

    public static Core.Heroes.Tariq.Tariq TariqOf(this StepDriver driver) => (Core.Heroes.Tariq.Tariq)driver.Hero;

    public static Core.Heroes.Rime.Rime RimeOf(this StepDriver driver) => (Core.Heroes.Rime.Rime)driver.Hero;

    /// <summary>Decides the procs <paramref name="force"/> answers (true or false) for the rest of the fight; the dice decide the others.</summary>
    public static ForcedProcs ForceProcs(this StepDriver driver, Func<string, bool?> force)
    {
        var procs = new ForcedProcs(driver.Context.Outcomes, force);
        driver.Context.Outcomes = procs;
        return procs;
    }

    /// <summary>The fight's random outcomes as the engine's own source, to switch its mode (the dice decide crits and damage spread).</summary>
    public static RandomOutcomeSource RandomOutcomes(this StepDriver driver) => (RandomOutcomeSource)driver.Context.Outcomes;

    /// <summary>Anima gains in order, one event per gain.</summary>
    public static List<StepEvent> AnimaGains(this StepDriver driver) =>
        driver.Events.Where(e => e.Kind == StepEventKind.ResourceGained && e.Name == "Anima").ToList();

    public static List<StepEvent> Damage(this StepDriver driver, string ability) =>
        driver.Events.Where(e => e.Kind == StepEventKind.Damage && e.Name == ability).ToList();
}

/// <summary>The fight's outcomes, except that <paramref name="forceProc"/> decides each proc it answers (null leaves it to the dice).</summary>
public sealed class ForcedProcs(IOutcomeSource inner, Func<string, bool?> forceProc) : IOutcomeSource
{
    /// <summary>Every proc rolled so far, forced or not: its key and the chance the engine gave it.</summary>
    public List<(string Key, double Chance)> Rolls { get; } = new();

    public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null)
    {
        Rolls.Add((procKey, chance));
        return forceProc(procKey) ?? inner.RollProc(procKey, chance, ctx, target);
    }

    public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) =>
        inner.SelectTargets(damageLogId, candidates, ctx);

    public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) =>
        inner.ChooseTarget(damageLogId, candidates, ctx, preferred);

    public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => inner.ResolveHit(hit, ctx);

    public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx) => inner.Land(landed, outcome, ctx);

    public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => inner.TickAt(tick, ctx);

    public void HitFinished(SimulationContext ctx) => inner.HitFinished(ctx);
}
