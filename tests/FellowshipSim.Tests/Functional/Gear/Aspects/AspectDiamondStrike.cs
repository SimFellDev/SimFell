using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Diamond Strike (aspect 33): "Your Damage has a chance (6.7 PPM) to conjure a Spectral Beam from above, dealing 72% / 127% / 182% / 237% Main Stat
/// damage to them. Diamond Strike applies an amplifying effect for 20 seconds on targets it strikes, increasing its damage to that target by 40%,
/// stacking up to 5 times" (and +35% per stack of Harmonious Soul, see <c>AspectComplexDiamondStrike</c>).
/// Dev: over the six heroes. No log has this aspect; every number is the tooltip's. Procs through <c>ForceProcs</c>; chances from <c>Rolls</c> (real
/// PPM: PPM x (1 + haste) x seconds since the last roll / 60). The beams are read from their effect (96) in the order they came.
/// </summary>
public class AspectDiamondStrike
{
    private const string ProcKey = "diamond_strike";
    private const int BeamEffect = 96;
    private static readonly double[] Coefficient = [0.72, 1.27, 1.82, 2.37];


    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> HeroRanks => AspectFights.HeroRanks;

    private static StepDriver Fight(GearHero hero, int rank = 1, double haste = 0, int enemies = 1, (FellowshipSim.Core.Customization.Gems.GemColor, double)[]? gems = null) =>
        hero.Fight(AspectFights.With(AspectFights.DiamondStrike, rank, haste: haste, enemies: enemies, gems: gems));

    private static ForcedProcs Force(StepDriver f, bool outcome) => f.ForceProcs(key => key == ProcKey ? outcome : null);

    private static List<StepEvent> Beams(StepDriver f) => f.AspectEffectHits(BeamEffect);

    private static IEnumerable<StepEvent> OtherHits(StepDriver f) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId != LogIds.Effect(BeamEffect));

    /// <summary>Casts the hero's unit hit until <paramref name="beams"/> beams have come (procs forced), each cast as soon as the hero can.</summary>
    private static void CastUntilBeams(StepDriver f, GearHero hero, int beams, EnemyTarget? target = null)
    {
        for (int i = 0; i < 40 && Beams(f).Count < beams; i++) AspectFights.CastWhenReady(f, hero, hero.Unit, target);
        Assert.True(Beams(f).Count >= beams, "enough hits for the beams asked for");
    }

    // ---- Chance

    /// <summary>
    /// Player: 6.7 procs per minute: a hit 6 seconds after the last roll rolls 6.7 x 6 / 60 (67%).
    /// Dev: 6 seconds of nothing, then one hit: its first roll against 6.7 x t / 60 (t the hit's time).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Chance_Is_6_7_Procs_Per_Minute(GearHero hero)
    {
        var f = Fight(hero);
        var procs = Force(f, false);
        f.Wait(6);
        hero.CastProbe(f, hero.Unit);
        double t = OtherHits(f).First().Time.TotalSeconds;
        Assert.Equal(6.7 * t / 60.0, procs.Rolls.First(r => r.Key == ProcKey).Chance, 1e-9);
    }

    /// <summary>
    /// Player: haste raises the chance in proportion.
    /// Dev: 50% haste and a shorter wait (2 seconds) so the chance stays under 1: 6.7 x 1.5 x t / 60.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Haste_Raises_The_Chance_In_Proportion(GearHero hero)
    {
        var f = Fight(hero, haste: 0.5);
        var procs = Force(f, false);
        f.Wait(2);
        hero.CastProbe(f, hero.Unit);
        double t = OtherHits(f).First().Time.TotalSeconds;
        Assert.Equal(6.7 * 1.5 * t / 60.0, procs.Rolls.First(r => r.Key == ProcKey).Chance, 1e-9);
    }

    // ---- The beam

    /// <summary>
    /// Player: no proc, no beam.
    /// Dev: the proc forced to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Proc_No_Beam(GearHero hero)
    {
        var f = Fight(hero);
        Force(f, false);
        hero.CastProbe(f, hero.Unit);
        Assert.Empty(Beams(f));
    }

    /// <summary>
    /// Player: the first beam on an enemy deals the rank's share of your Main Stat: 72% / 127% / 182% / 237% of 1000.
    /// Dev: the proc forced; the first beam. The hero's class balancing damage aura is on the beam
    /// (<see cref="AspectFights.ClassDamageAura"/>).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanks))]
    public void The_Beam_Deals_The_Rank_Share_Of_The_Main_Stat(GearHero hero, int rank)
    {
        var f = Fight(hero, rank);
        Force(f, true);
        hero.CastProbe(f, hero.Unit);
        Assert.Equal(Coefficient[rank - 1] * 1000.0 * AspectFights.ClassDamageAura(hero), Beams(f)[0].Value, 1e-6);
    }

    /// <summary>
    /// Player: each beam on an enemy amplifies the next by 40% (stacking up to 5 times): the 2nd beam deals 140% of the 1st, the 3rd 180%, ... the
    /// 6th and every one after 300%.
    /// Dev: the proc forced and the unit hit cast again until 8 beams have come; each beam over the first.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Each_Beam_Amplifies_The_Next_By_40_Percent_Up_To_Five_Stacks(GearHero hero)
    {
        var f = Fight(hero, 2);
        Force(f, true);
        CastUntilBeams(f, hero, 8);
        var beams = Beams(f);
        for (int k = 0; k < 8; k++) Assert.Equal(beams[0].Value * (1.0 + 0.4 * Math.Min(k, 5)), beams[k].Value, 1e-6);
    }

    /// <summary>
    /// Player: the amplifying effect lasts 20 seconds from the last beam, stacking to 5: after it ran out a beam is unamplified again.
    /// Dev: beams until the target has 5 stacks (read from its aura); procs then forced off (so a burn or bleed that ticks on cannot refresh it); the
    /// stacks are still there 19.5 seconds after the last beam and gone 20.5 seconds after; one more beam then equals the very first.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Amplification_Runs_Out_Twenty_Seconds_After_The_Last_Beam(GearHero hero)
    {
        var f = Fight(hero, 2);
        bool on = true;
        f.ForceProcs(key => key == ProcKey ? on : null);
        CastUntilBeams(f, hero, 6);
        double first = Beams(f)[0].Value;
        Assert.Equal(5, f.Enemy.Auras.GetStacks(DiamondStrike.Amplified, f.Now));
        on = false;
        double lastBeam = Beams(f).Last().Time.TotalSeconds;
        f.Wait(lastBeam + 19.5 - f.Now.TotalSeconds);
        Assert.Equal(5, f.Enemy.Auras.GetStacks(DiamondStrike.Amplified, f.Now));
        f.Wait(1.0);
        Assert.Equal(0, f.Enemy.Auras.GetStacks(DiamondStrike.Amplified, f.Now));
        on = true;
        int before = Beams(f).Count;
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        Assert.Equal(first, Beams(f)[before].Value, 1e-6);
    }

    /// <summary>
    /// Player: the amplification is on the enemy it struck: a second enemy's first beam is not amplified by the first's.
    /// Dev: two enemies; beams on the first until it has several; then a hit on the second: its first beam equals the first beam on the first enemy.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Amplification_Belongs_To_One_Enemy(GearHero hero)
    {
        var f = Fight(hero, 2, enemies: 2);
        Force(f, true);
        var second = f.Context.AllEnemies[1];
        CastUntilBeams(f, hero, 3, f.Enemy);
        AspectFights.CastWhenReady(f, hero, hero.Unit, second);
        var beams = Beams(f);
        Assert.Equal(beams[0].Value, beams.First(b => b.Target == second.Name).Value, 1e-6);
    }
}
