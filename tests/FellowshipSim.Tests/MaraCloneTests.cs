using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// A shadowy clone of Matriarch Macabre keeps Mara's numbers at the cast: an aura ending or starting after the cast leaves
/// both clones' hits as they were.
/// </summary>
public class MaraCloneTests
{
    private const double CastTime = 19.8;

    [Fact]
    public void Both_Clones_Keep_Matriarch_Macabre_When_It_Ends_After_The_Queens_Fang_Cast()
    {
        var (cast, clones) = CloneHits<QueensFang>(QueensFang.AbilityName, _ => { });

        Assert.Equal(2, clones.Count);
        Assert.All(clones, clone => Assert.Equal(cast * MatriarchMacabre.ClonePower, clone, 6));
    }

    [Fact]
    public void A_Buff_Gained_After_The_Cast_Adds_Nothing_To_The_Clones()
    {
        var buff = AuraDef.Effect("Damage after the cast", 999_001, 10.0, modifiers: StatModifier.More(StatType.DamageScale, 1.5));

        var (cast, clones) = CloneHits<QueensFang>(QueensFang.AbilityName, hero => hero.Auras.Apply(buff, SimTime.FromSeconds(CastTime)));

        Assert.All(clones, clone => Assert.Equal(cast * MatriarchMacabre.ClonePower, clone, 6));
    }

    [Fact]
    public void A_Clone_Keeps_Maiden_Of_Death_When_It_Ends_After_The_Cast()
    {
        var (cast, clones) = CloneHits<QueensFang>(QueensFang.AbilityName, _ => { },
            hero => hero.Auras.Apply(MaidenOfDeath.Buff, SimTime.FromSeconds(CastTime - MaidenOfDeath.BuffSeconds + 0.2)));

        Assert.All(clones, clone => Assert.Equal(cast * MatriarchMacabre.ClonePower, clone, 6));
    }

    /// <summary>
    /// The cast's hit and its clones' hits before crit and spread, from a finisher cast 0.2s before Matriarch Macabre ends
    /// with <paramref name="afterCast"/> running right after it.
    /// </summary>
    private static (double Cast, List<double> Clones) CloneHits<T>(string name, Action<Mara> afterCast, Action<Mara>? beforeCast = null) where T : Finisher
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        hero.CurrentTarget = enemy;
        var outcomes = new BaseDamages(ctx.Outcomes);
        ctx.Outcomes = outcomes;

        hero.Auras.Apply(MatriarchMacabre.Buff, ctx.CurrentTime);
        ctx.RunUntil(SimTime.FromSeconds(CastTime - MaidenOfDeath.BuffSeconds + 0.2));
        beforeCast?.Invoke(hero);
        ctx.RunUntil(SimTime.FromSeconds(CastTime));
        hero.GainComboPoints(6, ctx);
        Assert.True(ctx.BeginCast(hero, hero.GetAbility<T>()!, enemy).Succeeded);
        afterCast(hero);
        ctx.RunUntil(SimTime.FromSeconds(CastTime + 1.0));

        return (outcomes.Hits.Single(h => h.Name == name).Damage,
            outcomes.Hits.Where(h => h.Name == MatriarchMacabre.CloneHitName(name)).Select(h => h.Damage).ToList());
    }

    /// <summary>Passes every call to the random outcome source and keeps each hit's base damage as it lands.</summary>
    private sealed class BaseDamages(IOutcomeSource inner) : IOutcomeSource
    {
        public List<(string Name, double Damage)> Hits { get; } = new();
        public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null) => inner.RollProc(procKey, chance, ctx, target);
        public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) => inner.SelectTargets(damageLogId, candidates, ctx);
        public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) => inner.ChooseTarget(damageLogId, candidates, ctx, preferred);
        public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => inner.ResolveHit(hit, ctx);
        public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx)
        {
            Hits.Add((landed.AbilityName, landed.BaseDamage));
            return inner.Land(landed, outcome, ctx);
        }
        public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => inner.TickAt(tick, ctx);
        public void HitFinished(SimulationContext ctx) => inner.HitFinished(ctx);
        public SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => inner.ReactionDelay(hold, reactions, ctx);
    }
}
