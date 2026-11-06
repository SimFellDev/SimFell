using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>The chance of the game's RollProcPerMinute at a hero with no Haste.</summary>
public class RealPpmTests
{
    private const double Rate = 2.0;

    [Fact]
    public void A_Roll_Before_The_First_Proc_Has_At_Least_The_Floor()
    {
        var (hero, ctx, dice) = Fight();
        var ppm = new RealPpm("test", Rate);

        ctx.RunUntil(SimTime.FromSeconds(1));
        ppm.Roll(hero, ctx);

        Assert.Equal(Rate / RealPpm.FirstProcFloorDivisor, dice.Chances[^1], 9);
    }

    [Fact]
    public void After_The_First_Proc_The_Chance_Is_The_Rate_Over_The_Seconds_Since_The_Roll()
    {
        var (hero, ctx, dice) = Fight();
        var ppm = new RealPpm("test", Rate);
        dice.Proc = true;
        ctx.RunUntil(SimTime.FromSeconds(1));
        ppm.Roll(hero, ctx);
        dice.Proc = false;

        ctx.RunUntil(SimTime.FromSeconds(4));
        ppm.Roll(hero, ctx);

        Assert.Equal(Rate * 3.0 / 60.0, dice.Chances[^1], 9);
    }

    /// <summary>The drought factor passes 1 once enough time has passed since the proc, and it then multiplies the chance.</summary>
    [Fact]
    public void A_Drought_Multiplies_The_Chance()
    {
        var (hero, ctx, dice) = Fight();
        var ppm = new RealPpm("test", Rate);
        dice.Proc = true;
        ctx.RunUntil(SimTime.FromSeconds(1));
        ppm.Roll(hero, ctx);
        dice.Proc = false;

        ctx.RunUntil(SimTime.FromSeconds(59));
        ppm.Roll(hero, ctx);
        ctx.RunUntil(SimTime.FromSeconds(61));
        ppm.Roll(hero, ctx);

        Assert.Equal(2.5 * 2.0 / 60.0 * Rate, dice.Chances[^1], 9);
    }

    [Fact]
    public void A_Second_Roll_At_The_Same_Time_Has_No_Chance_After_The_First_Proc()
    {
        var (hero, ctx, dice) = Fight();
        var ppm = new RealPpm("test", Rate);
        dice.Proc = true;
        ctx.RunUntil(SimTime.FromSeconds(1));
        ppm.Roll(hero, ctx);
        dice.Proc = false;

        ppm.Roll(hero, ctx);

        Assert.Equal(0.0, dice.Chances[^1]);
    }

    [Fact]
    public void Haste_And_A_Blueprint_Factor_Scale_The_Rate()
    {
        var (hero, ctx, dice) = Fight();
        var haste = AuraDef.Internal("test haste", 0, 1, StatModifier.Add(StatType.Haste, 0.5));
        hero.Auras.Apply(haste, ctx.CurrentTime);
        double hasted = 1.0 + hero.GetHaste(ctx.CurrentTime);

        new RealPpm("hasted", Rate).Roll(hero, ctx);
        Assert.Equal(Rate * hasted / RealPpm.FirstProcFloorDivisor, dice.Chances[^1], 9);

        new RealPpm("unhasted", Rate, usesHaste: false).Roll(hero, ctx);
        Assert.Equal(Rate / RealPpm.FirstProcFloorDivisor, dice.Chances[^1], 9);

        new RealPpm("scaled", Rate, usesHaste: false, rateScale: _ => 1.25).Roll(hero, ctx);
        Assert.Equal(Rate * 1.25 / RealPpm.FirstProcFloorDivisor, dice.Chances[^1], 9);
    }

    private static (Hero Hero, SimulationContext Ctx, RecordingDice Dice) Fight()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var dice = new RecordingDice(ctx.Rng);
        ctx.Outcomes = dice;
        return (hero, ctx, dice);
    }

    private sealed class RecordingDice(SimulationRng rng) : RandomOutcomeSource(rng), IOutcomeSource
    {
        public bool Proc { get; set; }
        public List<double> Chances { get; } = [];

        public new bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null)
        {
            Chances.Add(chance);
            return Proc;
        }
    }
}
