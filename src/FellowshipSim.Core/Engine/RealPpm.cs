namespace FellowshipSim.Core.Engine;

/// <summary>What raises a <see cref="RealPpm"/> proc's rate: nothing, the hero's haste (× 1 + haste) or crit chance (× 1 + crit).</summary>
public enum PpmScaling
{
    None,
    Haste,
    Crit,
}

/// <summary>
/// A mechanism to control procs per minute (PPM) depsite the rate of proc attempts varying,
/// as introduced by World of Warcraft.
///
/// <list type="bullet">
/// <item>Each try's chance is the rate × the seconds since the last try (at most <see cref="MaxIntervalSeconds"/>) / 60, the
/// rate raised by <see cref="Scaling"/>. The fight's first try counts from the fight's start.</item>
/// <item>Bad luck protection: past 1.5 expected intervals (60 / rate) since the last proc, the chance is multiplied by
/// 1 + 3 × (intervals − 1.5). Each try adds at most <see cref="MaxIntervalSeconds"/> to the time since the last proc, which
/// counts up to <see cref="MaxBadLuckSeconds"/>.</item>
/// <item>First proc floor: until the first proc, a try's chance is at least the unscaled rate / 9, when
/// <see cref="SimulationContext.RealPpmFirstProcFloor"/> is on.</item>
/// </list>
/// The chance is capped at 1. Every new fight starts afresh.
/// </summary>
public class RealPpm
{
    public const double MaxIntervalSeconds = 1000.0;
    public const double MaxBadLuckSeconds = 1000.0;
    /// <summary>Bad luck protection starts this many expected intervals after the last proc.</summary>
    public const double BadLuckStartIntervals = 1.5;
    /// <summary>The multiplier grows by this much per expected interval past <see cref="BadLuckStartIntervals"/>.</summary>
    public const double BadLuckSlope = 3.0;
    /// <summary>Until the first proc, a try's chance is at least the unscaled rate divided by this.</summary>
    public const double FirstProcFloorDivisor = 9.0;

    public double BasePpm { get; }
    public PpmScaling Scaling { get; }
    public string ProcKey { get; }

    /// <summary>The time of the last try.</summary>
    public SimTime LastProcAttemptTime { get; private set; } = SimTime.Zero;
    /// <summary>The seconds counted since the last proc, each try adding at most <see cref="MaxIntervalSeconds"/>.</summary>
    public double SecondsSinceLastProc { get; private set; }
    /// <summary>True once it has procced in this fight.</summary>
    public bool HasProcced { get; private set; }

    public RealPpm(string procKey, double basePpm, PpmScaling scaling)
    {
        ProcKey = procKey;
        BasePpm = basePpm;
        Scaling = scaling;
    }

    /// <summary>The fight of the last try (each sim iteration and replay has its own).</summary>
    private SimulationContext? _fight;

    /// <summary>Tries to proc now, at <paramref name="hero"/>'s haste or crit chance.</summary>
    public bool Roll(Hero hero, SimulationContext ctx, EnemyTarget? target = null)
    {
        if (_fight != ctx)
        {
            Reset();
            _fight = ctx;
        }
        SimTime now = ctx.CurrentTime;
        double chance = Try(now, ScaleOf(hero, now), ctx.RealPpmFirstProcFloor);
        bool procced = ctx.Outcomes.RollProc(ProcKey, chance, ctx, target);
        Record(now, procced);
        return procced;
    }

    /// <summary>The factor <see cref="Scaling"/> raises the rate by: 1 + haste, 1 + crit chance, or 1.</summary>
    public double ScaleOf(Hero hero, SimTime now) => Scaling switch
    {
        PpmScaling.Haste => 1.0 + hero.GetHaste(now),
        PpmScaling.Crit => 1.0 + hero.CritChance(now),
        _ => 1.0,
    };

    /// <summary>
    /// The chance of a try at <paramref name="now"/>, the rate raised by <paramref name="scale"/>. Counts the time since the last
    /// try towards the bad luck protection; <see cref="Record"/> then records the try.
    /// </summary>
    public double Try(SimTime now, double scale, bool firstProcFloor)
    {
        double sinceLastTry = Math.Min((now - LastProcAttemptTime).TotalSeconds, MaxIntervalSeconds);
        SecondsSinceLastProc += sinceLastTry;

        double ppm = BasePpm * scale;
        double chance = ppm * sinceLastTry / 60.0;
        double intervals = Math.Min(SecondsSinceLastProc, MaxBadLuckSeconds) / (60.0 / ppm);
        chance *= Math.Max(1.0, 1.0 + (intervals - BadLuckStartIntervals) * BadLuckSlope);
        if (firstProcFloor && !HasProcced) chance = Math.Max(BasePpm / FirstProcFloorDivisor, chance);
        return Math.Min(chance, 1.0);
    }

    /// <summary>Records a try at <paramref name="now"/>; a proc starts the time since the last proc again.</summary>
    public void Record(SimTime now, bool procced)
    {
        LastProcAttemptTime = now;
        if (!procced) return;
        SecondsSinceLastProc = 0.0;
        HasProcced = true;
    }

    /// <summary>Starts afresh, as at the start of a fight.</summary>
    public void Reset()
    {
        LastProcAttemptTime = SimTime.Zero;
        SecondsSinceLastProc = 0.0;
        HasProcced = false;
    }
}
