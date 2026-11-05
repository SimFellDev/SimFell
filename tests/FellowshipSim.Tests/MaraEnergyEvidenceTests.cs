using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// A Venomous Delight proc is proved by the log's Energy above regeneration between neighbouring readings, with a cast's
/// cost counted back. The hero has no buff and no Haste here, so regeneration is <see cref="Mara.EnergyPerSecond"/>.
/// </summary>
public class MaraEnergyEvidenceTests
{
    private static LogResourceSnapshot Reading(double time, double energy, string kind = "damage", int? abilityId = null) => new()
    {
        Time = time,
        Event = kind,
        AbilityId = abilityId,
        Activation = kind == "cast",
        Resources = new() { ["2"] = new LogResourceValue { Amount = energy * 100, Max = 20000 } },
    };

    private static (LogOutcomeSource Log, SimulationContext Ctx) Replay(params LogResourceSnapshot[] readings)
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var script = new ReplayScript { Player = new ReplayPlayer { Id = 1, Hero = "Mara" } };
        script.Observations.Resources.AddRange(readings);
        var options = new ReplayOptions();
        return (new LogOutcomeSource(script, options, new HitLedger([]), new ReplayVerifier(script, options, hero)), ctx);
    }

    private static bool Roll(LogOutcomeSource log, SimulationContext ctx) => log.RollProc(VenomousDelight.ProcKey, VenomousDelight.Chance, ctx);

    private static double Regenerated(double seconds) => Mara.EnergyPerSecond * seconds;

    [Fact]
    public void A_Rise_Of_Regeneration_Alone_Proves_No_Proc()
    {
        var (log, ctx) = Replay(Reading(1.0, 100), Reading(1.5, 100 + Regenerated(0.5)));
        ctx.RunUntil(SimTime.FromSeconds(1.2));

        Assert.False(Roll(log, ctx));
    }

    [Fact]
    public void A_Rise_Of_One_Gain_Proves_One_Proc()
    {
        var (log, ctx) = Replay(Reading(1.0, 100), Reading(1.2, 100 + Regenerated(0.2) + VenomousDelight.EnergyGained));
        ctx.RunUntil(SimTime.FromSeconds(1.1));

        Assert.True(Roll(log, ctx));
        Assert.False(Roll(log, ctx));
    }

    [Fact]
    public void A_Rise_Of_Two_Gains_Proves_Two_Procs()
    {
        var (log, ctx) = Replay(Reading(1.0, 100), Reading(1.2, 100 + Regenerated(0.2) + 2 * VenomousDelight.EnergyGained));
        ctx.RunUntil(SimTime.FromSeconds(1.1));

        Assert.True(Roll(log, ctx));
        Assert.True(Roll(log, ctx));
        Assert.False(Roll(log, ctx));
    }

    [Fact]
    public void A_Rise_Over_A_Long_Gap_Between_Readings_Proves_A_Proc()
    {
        var (log, ctx) = Replay(Reading(1.0, 100), Reading(2.4, 100 + Regenerated(1.4) + VenomousDelight.EnergyGained));
        ctx.RunUntil(SimTime.FromSeconds(1.5));

        Assert.True(Roll(log, ctx));
    }

    [Fact]
    public void A_Rise_To_The_Maximum_Proves_One_Proc()
    {
        var (log, ctx) = Replay(Reading(1.0, 190), Reading(1.2, Mara.MaxEnergy));
        ctx.RunUntil(SimTime.FromSeconds(1.1));

        Assert.True(Roll(log, ctx));
        Assert.False(Roll(log, ctx));
    }

    [Fact]
    public void A_Rise_From_A_Cast_Counts_Its_Cost_Back()
    {
        double after = 150 - QueensFang.Cost + Regenerated(0.2) + VenomousDelight.EnergyGained;
        var (log, ctx) = Replay(Reading(1.0, 150, "cast", QueensFang.AbilityId), Reading(1.2, after));
        ctx.RunUntil(SimTime.FromSeconds(1.1));

        Assert.True(Roll(log, ctx));
    }

    [Fact]
    public void A_Rise_From_A_Casts_Reading_Proves_No_Proc_Before_The_Cast()
    {
        double after = 150 - QueensFang.Cost + Regenerated(0.05) + VenomousDelight.EnergyGained;
        var (log, ctx) = Replay(Reading(1.0, 150, "cast", QueensFang.AbilityId), Reading(1.05, after));
        ctx.RunUntil(SimTime.FromSeconds(0.98));

        Assert.False(Roll(log, ctx));
    }

    [Fact]
    public void Widows_Bites_Net_Energy_Cost_Is_Negative()
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });

        Assert.Equal(-WidowsBite.EnergyGained, hero.EnergySpentBy(WidowsBite.AbilityId));
        Assert.Equal(QueensFang.Cost, hero.EnergySpentBy(QueensFang.AbilityId));
        Assert.Equal(0, hero.EnergySpentBy(-1));
    }

    [Fact]
    public void Regenerating_Resources_Brings_Energy_To_The_Moment()
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        hero.SpendEnergy(100, ctx);
        ctx.RunUntil(SimTime.FromSeconds(0.25));

        hero.RegenerateResources(ctx.CurrentTime);

        Assert.Equal(100 + hero.EnergyRate(ctx.CurrentTime) * 0.25, hero.ResourceAmount(Mara.Energy), 6);
    }

    /// <summary>Energy 0.5s after <paramref name="change"/> runs, from 100 Energy.</summary>
    private static (double Energy, double Rate) EnergyAfter(Action<Mara, SimulationContext> change)
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        hero.SpendEnergy(100, ctx);
        change(hero, ctx);
        double rate = hero.EnergyRate(ctx.CurrentTime);
        ctx.RunUntil(SimTime.FromSeconds(0.5));
        return (hero.EnergyAt(ctx.CurrentTime), rate);
    }

    [Fact]
    public void A_Haste_Change_Costs_One_Server_Frame_Of_Regeneration()
    {
        var haste = AuraDef.Effect("Haste", 999_002, 10.0, modifiers: StatModifier.Add(StatType.Haste, 0.2));

        var (energy, rate) = EnergyAfter((hero, ctx) => hero.Auras.Apply(haste, ctx.CurrentTime));

        Assert.Equal(100 + rate * (0.5 - PeriodicEffect.ServerFrameSeconds), energy, tolerance: 0.02);
    }

    [Fact]
    public void A_Predators_Rush_Change_Costs_One_Server_Frame_Of_Regeneration()
    {
        var (energy, rate) = EnergyAfter((hero, ctx) => hero.Auras.Apply(SeethingPoison.PredatorsRush, ctx.CurrentTime));

        Assert.Equal(100 + rate * (0.5 - PeriodicEffect.ServerFrameSeconds), energy, tolerance: 0.02);
    }

    [Fact]
    public void A_Second_Change_During_The_Restart_Adds_No_Time()
    {
        var first = AuraDef.Effect("Haste", 999_002, 10.0, modifiers: StatModifier.Add(StatType.Haste, 0.2));
        var second = AuraDef.Effect("More Haste", 999_003, 10.0, modifiers: StatModifier.Add(StatType.Haste, 0.1));

        var (energy, rate) = EnergyAfter((hero, ctx) =>
        {
            hero.Auras.Apply(first, ctx.CurrentTime);
            hero.Auras.Apply(second, ctx.CurrentTime);
        });

        Assert.Equal(100 + rate * (0.5 - PeriodicEffect.ServerFrameSeconds), energy, tolerance: 0.02);
    }

    [Fact]
    public void Maiden_Of_Death_Multiplies_A_Gain_Through_Its_Function_Alone()
    {
        var (scaled, plain) = (0.0, 0.0);
        EnergyAfter((hero, ctx) =>
        {
            hero.Auras.Apply(MaidenOfDeath.Buff, ctx.CurrentTime);
            double before = hero.EnergyAt(ctx.CurrentTime);
            hero.GainEnergyWithMaiden(Hemorrhage.EnergyPerTick, ctx);
            scaled = hero.EnergyAt(ctx.CurrentTime) - before;
            hero.GainEnergy(VenomousDelight.EnergyGained, ctx);
            plain = hero.EnergyAt(ctx.CurrentTime) - before - scaled;
        });

        Assert.Equal(Hemorrhage.EnergyPerTick * MaidenOfDeath.EnergyRegen, scaled, 6);
        Assert.Equal(VenomousDelight.EnergyGained, plain, 6);
    }

    [Fact]
    public void A_Maiden_Of_Death_Change_Costs_No_Regeneration()
    {
        var (energy, rate) = EnergyAfter((hero, ctx) => hero.Auras.Apply(MaidenOfDeath.Buff, ctx.CurrentTime));

        Assert.Equal(100 + rate * 0.5, energy, 6);
    }
}
