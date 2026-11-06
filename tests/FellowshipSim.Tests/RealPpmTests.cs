using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Correctness checks for the real PPM mechanics: each try's chance, its bad luck protection and
/// first proc floor, the rate's scaling and the reset at each new fight. The tests that read a chance use a 6 PPM proc: one expected
/// proc every 10 seconds.
/// </summary>
public class RealPpmTests
{
    private const double Ppm = 6.0;
    private const double Interval = 60.0 / Ppm;

    private static SimTime At(double seconds) => SimTime.FromSeconds(seconds);

    /// <summary>A 6 PPM proc, with a try at 0s that missed: the next try counts from 0s, with no first proc floor.</summary>
    private static RealPpm TriedAtZero(PpmScaling scaling = PpmScaling.None)
    {
        var rppm = new RealPpm("test", Ppm, scaling);
        rppm.Try(At(0), scale: 1.0, firstProcFloor: false);
        rppm.Record(At(0), procced: false);
        return rppm;
    }

    // ---- The chance

    /// <summary>The chance grows with the time since the last try: the rate × the seconds / 60.</summary>
    [Theory]
    [InlineData(1.0, 0.1)]
    [InlineData(2.5, 0.25)]
    [InlineData(5.0, 0.5)]
    [InlineData(10.0, 1.0)]
    public void The_Chance_Is_The_Rate_Times_The_Time_Since_The_Last_Try(double seconds, double chance)
    {
        var rppm = TriedAtZero();

        Assert.Equal(chance, rppm.Try(At(seconds), scale: 1.0, firstProcFloor: false), 9);
    }

    /// <summary>A try counts the time since the last try, not since the last proc: a missed try at 5s leaves 0.3 at 8s.</summary>
    [Fact]
    public void A_Missed_Try_Starts_The_Time_Since_The_Last_Try_Again()
    {
        var rppm = TriedAtZero();
        rppm.Try(At(5), scale: 1.0, firstProcFloor: false);
        rppm.Record(At(5), procced: false);

        Assert.Equal(0.3, rppm.Try(At(8), scale: 1.0, firstProcFloor: false), 9);
    }

    /// <summary>The chance is capped at 1.</summary>
    [Fact]
    public void The_Chance_Is_Capped_At_1()
    {
        var rppm = TriedAtZero();

        Assert.Equal(1.0, rppm.Try(At(25), scale: 1.0, firstProcFloor: false));
    }

    /// <summary>A scale raises the rate: at a scale of 1.5 (50% haste or crit), 2 seconds give 0.3.</summary>
    [Fact]
    public void A_Scale_Raises_The_Rate()
    {
        var rppm = TriedAtZero();

        Assert.Equal(0.3, rppm.Try(At(2), scale: 1.5, firstProcFloor: false), 9);
    }

    /// <summary>
    /// At most <see cref="RealPpm.MaxIntervalSeconds"/> since the last try count: a proc so rare that it never reaches 1 or its bad luck
    /// protection gets the same chance at the cap and past it.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(500.0)]
    public void At_Most_The_Interval_Cap_Since_The_Last_Try_Counts(double pastTheCap)
    {
        const double rarePpm = 0.006; // one expected proc every 10,000s
        var rppm = new RealPpm("test", rarePpm, PpmScaling.None);
        rppm.Record(At(0), procced: false);

        double chance = rppm.Try(At(RealPpm.MaxIntervalSeconds + pastTheCap), scale: 1.0, firstProcFloor: false);

        Assert.Equal(rarePpm * RealPpm.MaxIntervalSeconds / 60, chance, 9);
    }

    /// <summary>The fight's first try counts the time from the fight's start.</summary>
    [Fact]
    public void The_First_Try_Counts_From_The_Fights_Start()
    {
        var rppm = new RealPpm("test", Ppm, PpmScaling.None);

        Assert.Equal(0.4, rppm.Try(At(4), scale: 1.0, firstProcFloor: false), 9);
    }

    // ---- Bad luck protection

    /// <summary>
    /// Past 1.5 expected intervals since the last proc, the chance is multiplied by 1 + 3 × (intervals − 1.5). A 6 PPM proc tried every
    /// 2 seconds with no proc: × 1 at 14s (1.4 intervals), × 1.3 at 16s, × 1.9 at 18s, × 2.5 at 20s.
    /// </summary>
    [Theory]
    [InlineData(14.0, 0.2)]
    [InlineData(16.0, 0.2 * 1.3)]
    [InlineData(18.0, 0.2 * 1.9)]
    [InlineData(20.0, 0.2 * 2.5)]
    public void Past_1_5_Intervals_Since_The_Last_Proc_The_Chance_Grows_3_Times_Faster(double seconds, double chance)
    {
        var rppm = TriedAtZero();
        for (double t = 2; t < seconds; t += 2)
        {
            rppm.Try(At(t), scale: 1.0, firstProcFloor: false);
            rppm.Record(At(t), procced: false);
        }

        Assert.Equal(chance, rppm.Try(At(seconds), scale: 1.0, firstProcFloor: false), 9);
    }

    /// <summary>A proc starts the time since the last proc again: no bad luck protection right after it.</summary>
    [Fact]
    public void A_Proc_Ends_The_Bad_Luck_Protection()
    {
        var rppm = TriedAtZero();
        for (double t = 2; t <= 18; t += 2)
        {
            rppm.Try(At(t), scale: 1.0, firstProcFloor: false);
            rppm.Record(At(t), procced: t == 18);
        }

        Assert.Equal(0.2, rppm.Try(At(20), scale: 1.0, firstProcFloor: false), 9);
    }

    /// <summary>Each try adds at most <see cref="RealPpm.MaxIntervalSeconds"/> to the time since the last proc.</summary>
    [Fact]
    public void Each_Try_Adds_At_Most_The_Interval_Cap_To_The_Time_Since_The_Last_Proc()
    {
        var rppm = new RealPpm("test", 0.5, PpmScaling.None);

        rppm.Try(At(RealPpm.MaxIntervalSeconds + 500), scale: 1.0, firstProcFloor: false);

        Assert.Equal(RealPpm.MaxIntervalSeconds, rppm.SecondsSinceLastProc, 9);
    }

    /// <summary>The time since the last proc counts up to 1000 seconds: a 0.06 PPM proc (1000s interval) never gets past × 1.</summary>
    [Fact]
    public void The_Time_Since_The_Last_Proc_Counts_Up_To_1000_Seconds()
    {
        var rppm = new RealPpm("test", 0.06, PpmScaling.None);
        for (double t = 30; t <= 3000; t += 30)
        {
            rppm.Try(At(t), scale: 1.0, firstProcFloor: false);
            rppm.Record(At(t), procced: false);
        }

        // 1000s is 1 interval, under the 1.5 where the protection starts
        Assert.Equal(0.06 * 30 / 60, rppm.Try(At(3030), scale: 1.0, firstProcFloor: false), 9);
    }

    // ---- First proc floor

    /// <summary>Until the first proc, a try's chance is at least the rate / 9: 6 PPM tried twice at once gives 2/3, then 2/3.</summary>
    [Fact]
    public void Until_The_First_Proc_The_Chance_Is_At_Least_The_Rate_Over_9()
    {
        var rppm = TriedAtZero();

        double first = rppm.Try(At(1), scale: 1.0, firstProcFloor: true);
        rppm.Record(At(1), procced: false);
        double second = rppm.Try(At(1), scale: 1.0, firstProcFloor: true);

        Assert.Equal(Ppm / 9, first, 9);
        Assert.Equal(Ppm / 9, second, 9);
    }

    /// <summary>The floor is the unscaled rate / 9: haste or crit does not raise it.</summary>
    [Fact]
    public void The_Floor_Is_The_Unscaled_Rate_Over_9()
    {
        var rppm = new RealPpm("test", 0.9, PpmScaling.Haste);

        Assert.Equal(0.1, rppm.Try(At(0), scale: 2.0, firstProcFloor: true), 9);
    }

    /// <summary>After the first proc, the floor no longer applies.</summary>
    [Fact]
    public void After_The_First_Proc_The_Floor_No_Longer_Applies()
    {
        var rppm = TriedAtZero();
        rppm.Record(At(0), procced: true);

        Assert.Equal(0.1, rppm.Try(At(1), scale: 1.0, firstProcFloor: true), 9);
    }

    /// <summary>With the floor off, the first try is the rate × the time alone.</summary>
    [Fact]
    public void With_The_Floor_Off_The_First_Try_Is_The_Rate_Times_The_Time()
    {
        var rppm = TriedAtZero();

        Assert.Equal(0.1, rppm.Try(At(1), scale: 1.0, firstProcFloor: false), 9);
    }

    // ---- In a fight

    /// <summary>The rate's scale is 1 + haste, 1 + crit chance, or 1, read from the hero.</summary>
    [Fact]
    public void The_Scale_Is_1_Plus_Haste_Or_Crit_Or_1()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, HasteRating = 500, CritRating = 300 });
        _ = new SimulationContext(hero);

        Assert.Equal(1.0 + hero.GetHaste(At(0)), new RealPpm("test", Ppm, PpmScaling.Haste).ScaleOf(hero, At(0)));
        Assert.Equal(1.0 + hero.CritChance(At(0)), new RealPpm("test", Ppm, PpmScaling.Crit).ScaleOf(hero, At(0)));
        Assert.Equal(1.0, new RealPpm("test", Ppm, PpmScaling.None).ScaleOf(hero, At(0)));
        Assert.True(hero.GetHaste(At(0)) > 0 && hero.CritChance(At(0)) > 0, "the hero has haste and crit");
    }

    /// <summary>A roll asks the fight's outcome source with the chance, and the context's switch turns the first proc floor off.</summary>
    [Theory]
    [InlineData(true, Ppm / 9)]
    [InlineData(false, 0.1)]
    public void A_Roll_Uses_The_Fights_First_Proc_Floor_Switch(bool floor, double chance)
    {
        var (hero, ctx, chances) = Fight();
        ctx.RealPpmFirstProcFloor = floor;
        var rppm = new RealPpm("test", Ppm, PpmScaling.None);

        RollAt(rppm, hero, ctx, 1.0);

        Assert.Equal(chance, chances.Single(), 9);
    }

    /// <summary>A new fight starts afresh: its first try counts from its own start, before any proc.</summary>
    [Fact]
    public void A_New_Fight_Starts_Afresh()
    {
        var rppm = new RealPpm("test", Ppm, PpmScaling.None);
        var (hero, first, _) = Fight(procs: true);
        first.RealPpmFirstProcFloor = false;
        RollAt(rppm, hero, first, 8.0);

        var (hero2, second, chances) = Fight();
        RollAt(rppm, hero2, second, 1.0);

        Assert.Equal(Ppm / 9, chances.Single(), 9); // the floor is back: no proc yet in this fight
        Assert.False(rppm.HasProcced);
    }

    /// <summary>A fight against a training dummy whose outcome source records each proc chance, every proc landing or none.</summary>
    private static (Hero Hero, SimulationContext Ctx, List<double> Chances) Fight(bool procs = false)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        var chances = new List<double>();
        ctx.Outcomes = new RecordingProcs(ctx.Outcomes, chances, procs);
        return (hero, ctx, chances);
    }

    private static void RollAt(RealPpm rppm, Hero hero, SimulationContext ctx, double seconds)
    {
        ctx.Schedule(At(seconds), c => rppm.Roll(hero, c));
        ctx.RunUntil(At(seconds));
    }

    /// <summary>Records each proc chance and lands every proc or none; passes every other call on.</summary>
    private sealed class RecordingProcs(IOutcomeSource inner, List<double> chances, bool procs) : IOutcomeSource
    {
        public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null)
        {
            chances.Add(chance);
            return procs;
        }
        public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) => inner.SelectTargets(damageLogId, candidates, ctx);
        public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) => inner.ChooseTarget(damageLogId, candidates, ctx, preferred);
        public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => inner.ResolveHit(hit, ctx);
        public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx) => inner.Land(landed, outcome, ctx);
        public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => inner.TickAt(tick, ctx);
        public void HitFinished(SimulationContext ctx) => inner.HitFinished(ctx);
        public SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => inner.ReactionDelay(hold, reactions, ctx);
    }
}
