using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

public class MaraHemorrhagePeriodTests
{
    [Fact]
    public void A_Copy_Of_Gushing_Blood_Ticks_With_From_The_Shadows_Twice_And_Bloodrush()
    {
        var (hero, ctx, struck, other) = Setup();
        hero.Auras.Apply(MaidenOfDeath.Buff, ctx.CurrentTime);
        var ticks = TickTimes(hero, other);

        hero.Hemorrhage.ApplyFromStrike(ctx, struck, 30.0);
        ctx.RunUntil(SimTime.FromSeconds(10.0));

        AssertGaps(ticks, 3.0 / (1.0 + hero.GetHaste(ctx.CurrentTime)) * 0.87 * 0.87 / 1.2);
    }

    [Fact]
    public void A_Bleed_Outside_Maiden_Of_Death_Ticks_With_From_The_Shadows_Once_And_Bloodrush()
    {
        var (hero, ctx, struck, _) = Setup();
        var ticks = TickTimes(hero, struck);

        hero.Hemorrhage.ApplyFromStrike(ctx, struck, 30.0);
        ctx.RunUntil(SimTime.FromSeconds(10.0));

        AssertGaps(ticks, 3.0 / (1.0 + hero.GetHaste(ctx.CurrentTime)) * 0.87 / 1.2);
    }

    private static (Mara Hero, SimulationContext Ctx, EnemyTarget Struck, EnemyTarget Other) Setup()
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild
        {
            Hero = "Mara",
            PrimaryStat = 1000,
            Talents = [GushingBlood.TalentId, Bloodrush.TalentId],
            LegendaryItemIds = [FromTheShadows.HeadItemId],
        });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var struck = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        var other = ctx.AddEnemy(101, 1, "Dummy", 1e12);
        return (hero, ctx, struck, other);
    }

    private static List<double> TickTimes(Mara hero, EnemyTarget enemy)
    {
        var ticks = new List<double>();
        hero.OnDamageDealt += (in DamageContext hit) =>
        {
            if (hit.AbilityName == Hemorrhage.EffectName && hit.Target == enemy) ticks.Add(hit.Timestamp.TotalSeconds);
        };
        return ticks;
    }

    private static void AssertGaps(List<double> ticks, double period)
    {
        Assert.True(ticks.Count >= 3, $"{ticks.Count} ticks");
        for (int i = 1; i < ticks.Count; i++) Assert.Equal(period, ticks[i] - ticks[i - 1], 0.0011);
    }
}
