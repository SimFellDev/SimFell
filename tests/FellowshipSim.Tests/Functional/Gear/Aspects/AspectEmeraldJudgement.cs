using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Emerald Judgement (aspect 34): "damage has a chance (0.8 / 1.3 / 1.8 / 2.3 procs per minute by rank) to conjure an Emerald Spike for 800% Main Stat
/// damage" under the enemy that was hit.
/// Dev: over the six heroes. Procs are decided with <c>ForceProcs</c>; chances are read from <c>Rolls</c>. Real PPM: the chance of a roll is
/// PPM x (1 + haste) x (seconds since the previous roll) / 60, the first roll counting from the start of the fight, so a hit after a long wait
/// gives an exact, unclamped chance. 1000 Main Stat and no crit unless said.
/// </summary>
public class AspectEmeraldJudgement
{
    private const string ProcKey = "emerald_judgement";
    private const int SpikeEffect = 93;
    private static readonly double[] ProcsPerMinute = [0.8, 1.3, 1.8, 2.3];

    public static IEnumerable<object[]> Heroes => GearHero.All;
    public static IEnumerable<object[]> HeroRanks => AspectFights.HeroRanks;

    private static StepDriver Fight(GearHero hero, int rank = 1, double haste = 0, double crit = 0, double expertise = 0, double mainStat = 1000, int enemies = 1)
    {
        var loadout = new GearLoadout
        {
            Augmentations = [(AspectFights.EmeraldJudgement, rank)], Haste = haste, Crit = crit, Expertise = expertise, MainStat = mainStat, Enemies = enemies,
        };
        return hero.Fight(loadout);
    }

    private static ForcedProcs Force(StepDriver f, bool outcome) => f.ForceProcs(key => key == ProcKey ? outcome : null);

    private static List<StepEvent> Spikes(StepDriver f) => f.AspectEffectHits(SpikeEffect);

    private static List<StepEvent> OtherHits(StepDriver f) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId != LogIds.Effect(SpikeEffect)).ToList();

    // ---- Chance

    /// <summary>
    /// Player: the aspect's chance is its rank's procs per minute, spread over time: a hit 10 seconds after the last roll rolls PPM x 10 / 60.
    /// Dev: a 10-second wait with nothing happening, then one hit; its first roll against PPM x t / 60 (t the time of the hit).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanks))]
    public void The_Chance_Is_The_Rank_PPM_Over_The_Time_Since_The_Last_Roll(GearHero hero, int rank)
    {
        var f = Fight(hero, rank);
        var procs = Force(f, false);
        f.Wait(10);
        hero.CastProbe(f, hero.Unit);
        double t = OtherHits(f).First().Time.TotalSeconds;
        var rolls = procs.Rolls.Where(r => r.Key == ProcKey).ToList();
        Assert.Equal(ProcsPerMinute[rank - 1] * t / 60.0, rolls[0].Chance, 1e-9);
    }

    /// <summary>
    /// Player: haste raises the proc chance of a procs-per-minute effect in proportion.
    /// Dev: 50% haste, rank 1: the first roll after 10 seconds against 0.8 x 1.5 x t / 60.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Haste_Raises_The_Chance_In_Proportion(GearHero hero)
    {
        var f = Fight(hero, 1, haste: 0.5);
        var procs = Force(f, false);
        f.Wait(10);
        hero.CastProbe(f, hero.Unit);
        double t = OtherHits(f).First().Time.TotalSeconds;
        var rolls = procs.Rolls.Where(r => r.Key == ProcKey).ToList();
        Assert.Equal(0.8 * 1.5 * t / 60.0, rolls[0].Chance, 1e-9);
    }

    // ---- The spike

    /// <summary>
    /// Player: a failed roll conjures no spike.
    /// Dev: the proc forced to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Proc_No_Spike(GearHero hero)
    {
        var f = Fight(hero);
        Force(f, false);
        hero.CastProbe(f, hero.Unit);
        f.Wait(3);
        Assert.Empty(Spikes(f));
    }

    /// <summary>
    /// Player: the spike deals 800% of your Main Stat (8 x 1000 = 8000), on top of every rank.
    /// Dev: the proc forced; the first spike, on a hero with no crit and no Expertise, times the hero's class balancing damage aura.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanks))]
    public void The_Spike_Deals_Eight_Times_The_Main_Stat(GearHero hero, int rank)
    {
        var f = Fight(hero, rank);
        Force(f, true);
        hero.CastProbe(f, hero.Unit);
        Assert.Equal(8000.0 * AspectFights.ClassDamageAura(hero), Spikes(f)[0].Value, 1e-6);
    }

    /// <summary>
    /// Player: the spike grows with your Main Stat and with Expertise.
    /// Dev: 2500 Main Stat and 40% Expertise: 8 x 2500 x 1.4 = 28000, times the class aura.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Spike_Scales_With_Main_Stat_And_Expertise(GearHero hero)
    {
        var f = Fight(hero, mainStat: 2500, expertise: 0.4);
        Force(f, true);
        hero.CastProbe(f, hero.Unit);
        Assert.Equal(28000.0 * AspectFights.ClassDamageAura(hero), Spikes(f)[0].Value, 1e-6);
    }

    /// <summary>
    /// Player: the spike can crit, with your crit chance.
    /// Dev: 30% crit chance; the spike's event carries a 0.3 crit chance.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Spike_Can_Crit(GearHero hero)
    {
        var f = Fight(hero, crit: 0.3);
        Force(f, true);
        hero.CastProbe(f, hero.Unit);
        Assert.Equal(0.3, Spikes(f)[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: each hit rolls once and the spike's own damage does not roll: with every roll succeeding, there is one spike per other hit, not a chain.
    /// Dev: the proc forced; the spikes' count against the count of the other damage events (periodic ticks included).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Spike_Does_Not_Trigger_Another(GearHero hero)
    {
        var f = Fight(hero);
        var procs = Force(f, true);
        hero.CastProbe(f, hero.Unit);
        f.Wait(5);
        Assert.Equal(OtherHits(f).Count, Spikes(f).Count);
        Assert.Equal(OtherHits(f).Count, procs.Rolls.Count(r => r.Key == ProcKey));
    }

    /// <summary>
    /// Player: the spike erupts under the enemy that was hit.
    /// Dev: two enemies, the hero casting at the second; with every roll succeeding, the spikes' targets are exactly the targets of the hits.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Spike_Erupts_Under_The_Enemy_That_Was_Hit(GearHero hero)
    {
        var f = Fight(hero, enemies: 2);
        Force(f, true);
        GearFights.Fill(f);
        f.Cast(hero.Get(f, hero.Unit), f.Context.AllEnemies[1]);
        f.Wait(3);
        Assert.Equal(OtherHits(f).Select(h => h.Target).Order(), Spikes(f).Select(h => h.Target).Order());
    }
}
