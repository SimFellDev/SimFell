using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Maiden of Death's 6 Combo Points come from a generator's hits, at the cast's state of the buff.</summary>
public class MaraComboPointTests
{
    [Fact]
    public void A_Skittering_Blades_With_No_Hit_Generates_No_Combo_Points_In_Maiden_Of_Death()
    {
        var (hero, _) = Cast(strikes: false, hero => hero.Auras.Apply(MaidenOfDeath.Buff, SimTime.Zero));

        Assert.Equal(0, hero.ComboPointsHeld);
    }

    [Fact]
    public void A_Skittering_Blades_With_A_Hit_Generates_Six_Combo_Points_In_Maiden_Of_Death()
    {
        var (hero, _) = Cast(strikes: true, hero => hero.Auras.Apply(MaidenOfDeath.Buff, SimTime.Zero));

        Assert.Equal(MaidenOfDeath.ComboPointsPerGenerator, hero.ComboPointsHeld);
    }

    [Fact]
    public void A_Hit_After_Maiden_Of_Death_Ends_Keeps_The_Casts_Six_Combo_Points()
    {
        var (hero, _) = Cast(strikes: true, hero => hero.Auras.Apply(MaidenOfDeath.Buff, SimTime.Zero),
            afterCast: hero => hero.Auras.Remove(MaidenOfDeath.Buff, SimTime.FromSeconds(0.1)));

        Assert.Equal(MaidenOfDeath.ComboPointsPerGenerator, hero.ComboPointsHeld);
    }

    /// <summary>One Skittering Blades on one enemy at 0.1s, each hit landing 1ms later; with no strikes, on no enemy.</summary>
    private static (Mara Hero, SimulationContext Context) Cast(bool strikes, Action<Mara> beforeCast, Action<Mara>? afterCast = null)
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        hero.CurrentTarget = enemy;
        ctx.Outcomes = new HitsLateOrNever(ctx.Outcomes, strikes);

        beforeCast(hero);
        ctx.RunUntil(SimTime.FromSeconds(0.1));
        Assert.True(ctx.BeginCast(hero, hero.GetAbility<SkitteringBlades>()!, enemy).Succeeded);
        afterCast?.Invoke(hero);
        ctx.RunUntil(SimTime.FromSeconds(0.2));
        return (hero, ctx);
    }

    /// <summary>The random outcome source, with hits landing 1ms after their release, or no target for an area hit.</summary>
    private sealed class HitsLateOrNever(IOutcomeSource inner, bool strikes) : IOutcomeSource
    {
        public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null) => inner.RollProc(procKey, chance, ctx, target);
        public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) => strikes ? candidates : [];
        public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) => inner.ChooseTarget(damageLogId, candidates, ctx, preferred);
        public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => inner.ResolveHit(hit, ctx) with { LandingDelay = SimTime.FromSeconds(0.001) };
        public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx) => inner.Land(landed, outcome, ctx);
        public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => inner.TickAt(tick, ctx);
        public double RollDuration(in EffectDurationRequest application, SimulationContext ctx) => inner.RollDuration(application, ctx);
        public double RollDelay(double maxSeconds, SimulationContext ctx) => inner.RollDelay(maxSeconds, ctx);
        public void HitFinished(SimulationContext ctx) => inner.HitFinished(ctx);
        public SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => inner.ReactionDelay(hold, reactions, ctx);
    }
}
