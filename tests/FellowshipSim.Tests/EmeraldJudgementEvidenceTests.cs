using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>An Emerald Judgement spike lands with the hit that rolled it, so a log spike proves a roll within a server frame of it.</summary>
public class EmeraldJudgementEvidenceTests
{
    private static LogDamage Spike(double time, EnemyTarget enemy) => new()
    {
        Time = time,
        AbilityId = LogIds.Effect(EmeraldJudgement.DamageEffectId),
        Target = new UnitRef(enemy.Id, enemy.InstanceId),
    };

    private static (LogOutcomeSource Log, SimulationContext Ctx, EnemyTarget Enemy) Replay(double spikeTime)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        var script = new ReplayScript { Player = new ReplayPlayer { Id = 1, Hero = "Mara" } };
        var options = new ReplayOptions();
        var ledger = new HitLedger([Spike(spikeTime, enemy)]);
        return (new LogOutcomeSource(script, options, ledger, new ReplayVerifier(script, options, hero)), ctx, enemy);
    }

    [Theory]
    [InlineData(1.000)]
    [InlineData(1.016)]
    [InlineData(0.984)]
    public void A_Spike_In_The_Rolls_Frame_Proves_The_Proc(double spikeTime)
    {
        var (log, ctx, enemy) = Replay(spikeTime);
        ctx.RunUntil(SimTime.FromSeconds(1.0));

        Assert.True(log.RollProc(EmeraldJudgement.ProcKey, 0.01, ctx, enemy));
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(0.5)]
    public void A_Spike_Outside_The_Rolls_Frame_Proves_No_Proc(double spikeTime)
    {
        var (log, ctx, enemy) = Replay(spikeTime);
        ctx.RunUntil(SimTime.FromSeconds(1.0));

        Assert.False(log.RollProc(EmeraldJudgement.ProcKey, 0.01, ctx, enemy));
    }
}
