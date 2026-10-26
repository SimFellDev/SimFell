using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Diamond Strike with the Diamond gem's Harmonious Soul: "Diamond Strike gains an additional +35% increased Damage for every stack of Harmonious Soul you
/// have active". Harmonious Soul (Diamond gem node 1, from 80 gem power): each enemy you defeat gives +0.6% Critical Strike, Haste, Expertise and Spirit
/// for 5 seconds, stacking up to 4 times.
/// Dev: over the six heroes. 100 Diamond gem power (only that node is active). Enemies are defeated (added and removed as defeated) to build
/// stacks, with Diamond Strike's procs off; then the procs are forced on and the hero hits the real enemy. The beam also grows with the Expertise and the Critical Strike
/// chance Harmonious Soul gives (0.6% a stack each; as every ability's hit it can crit, and a crit does 200% in expected-value mode, so 0.6% crit
/// chance is 0.6% damage), so the expected ratio over a beam with no stacks is (1 + 0.35 x stacks) x (1 + 0.006 x stacks) x (1 + 0.006 x stacks).
/// </summary>
public class AspectComplexDiamondStrike
{
    private const string ProcKey = "diamond_strike";
    private const int BeamEffect = 96;

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> HeroKills =>
        from hero in GearFights.Heroes from kills in new[] { 1, 3, 6 } select new object[] { hero, kills };

    private static StepDriver Fight(GearHero hero) =>
        hero.Fight(AspectFights.With(AspectFights.DiamondStrike, 2, gems: [(GemColor.Diamond, 100)]));

    /// <summary>An enemy is defeated: the engine's fights end enemies through <c>RemoveEnemy</c> (a replay does it from the log), so the test does the same.</summary>
    private static void Kill(StepDriver f)
    {
        var weak = f.AddEnemy(1);
        f.Context.RemoveEnemy(weak, defeated: true);
    }

    private static int Stacks(StepDriver f) => f.Hero.Auras.GetStacks(Diamond.HarmoniousSoulSmall, f.Now);

    private static double FirstBeam(StepDriver f) => f.AspectEffectHits(BeamEffect).First().Value;

    /// <summary>
    /// Player: each Harmonious Soul stack (up to 4) makes the beam deal 35% more.
    /// Dev: the beam with k kills' stacks over the beam with none, k = 1, 3 and 6 (capped at 4 stacks).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroKills))]
    public void Each_Harmonious_Soul_Stack_Adds_35_Percent_To_The_Beam(GearHero hero, int kills)
    {
        var baseline = Fight(hero);
        baseline.ForceProcs(key => key == ProcKey ? true : null);
        hero.CastProbe(baseline, hero.Unit);
        double unamplified = FirstBeam(baseline);

        var f = Fight(hero);
        bool on = false;
        f.ForceProcs(key => key == ProcKey ? on : null);
        for (int i = 0; i < kills; i++) Kill(f);
        int stacks = Stacks(f);
        Assert.Equal(Math.Min(kills, 4), stacks);
        on = true;
        int before = f.AspectEffectHits(BeamEffect).Count;
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        double beam = f.AspectEffectHits(BeamEffect)[before].Value;
        Assert.Equal((1.0 + 0.35 * stacks) * (1.0 + 0.006 * stacks) * (1.0 + 0.006 * stacks), beam / unamplified, 1e-6);
    }

    /// <summary>
    /// Player: the two bonuses multiply: with 2 Harmonious Soul stacks the second beam on an enemy (one amplification stack, +40%) deals 1.4 times the first.
    /// Dev: two kills, then beams on the real enemy.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Amplification_And_The_Soul_Multiply(GearHero hero)
    {
        var f = Fight(hero);
        bool on = false;
        f.ForceProcs(key => key == ProcKey ? on : null);
        Kill(f);
        Kill(f);
        on = true;
        int before = f.AspectEffectHits(BeamEffect).Count;
        // the soul's stacks only drop at their 5 second expiry, which the two casts stay inside
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        Assert.Equal(2, Stacks(f));
        var beams = f.AspectEffectHits(BeamEffect).Skip(before).Where(b => b.Target == f.Enemy.Name).ToList();
        Assert.True(beams.Count >= 2, "enough hits for two beams");
        Assert.Equal(1.4, beams[1].Value / beams[0].Value, 1e-6);
    }
}
