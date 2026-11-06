namespace FellowshipSim.Core.Engine;

/// <summary>
/// A proc-per-minute roll: CRRandomStreamComponent::RollProcPerMinute 0x1452CBD20, build 26320967. With R the hasted rate,
/// the chance is max(1, 0.05 × R × min(t since the last proc, 1000) − 3.5) × (t since the last roll) / 60 × R, and at least
/// R / 8 until the stream's first proc (0x1452CBFE1 to 0x1452CC00B). The stream's times start at the dungeon's start, and
/// nothing clears them or the first proc within a run (0x1452CBE9F, 0x1452CC08E).
/// </summary>
public class RealPpm
{
    /// <summary>Slope and offset of the drought factor, on R × seconds (constants 0x147C0A0EC and 0x148D37F24).</summary>
    public const double DroughtSlope = 0.05;
    public const double DroughtOffset = 3.5;

    /// <summary>The longest drought the factor counts (constant 0x1487EAA40).</summary>
    public const double LongestDroughtSeconds = 1000.0;

    /// <summary>FirstProcChanceFloorPPMDivisor, default 8 (0x1452C220F).</summary>
    public const double FirstProcFloorDivisor = 8.0;

    public double BasePpm { get; }
    public SimTime LastProcAttemptTime { get; private set; } = SimTime.Zero;

    public string ProcKey { get; }

    private readonly bool _usesHaste;
    private readonly Func<SimTime, double>? _rateScale;
    private SimTime _lastProc = SimTime.Zero;
    private bool _procced;

    /// <param name="usesHaste">The roll's bUseHaste: the game data's scalesWithHaste.</param>
    /// <param name="rateScale">A Blueprint's own factor on the rate at each roll (the game data's formula).</param>
    public RealPpm(string procKey, double basePpm, bool usesHaste = true, Func<SimTime, double>? rateScale = null)
    {
        ProcKey = procKey;
        BasePpm = basePpm;
        _usesHaste = usesHaste;
        _rateScale = rateScale;
    }

    /// <summary>The fight of the last attempt (each sim iteration and replay has its own). A new fight starts at time 0.</summary>
    private SimulationContext? _fight;

    public bool Roll(Hero hero, SimulationContext ctx, EnemyTarget? target = null)
    {
        if (_fight != ctx)
        {
            LastProcAttemptTime = SimTime.Zero;
            _lastProc = SimTime.Zero;
            _procced = false;
            _fight = ctx;
        }
        SimTime now = ctx.CurrentTime;
        double secondsSinceRoll = (now - LastProcAttemptTime).TotalSeconds;
        double secondsSinceProc = Math.Min((now - _lastProc).TotalSeconds, LongestDroughtSeconds);
        LastProcAttemptTime = now;

        double rate = BasePpm * (_rateScale?.Invoke(now) ?? 1.0) * (_usesHaste ? 1.0 + hero.GetHaste(now) : 1.0);
        double drought = Math.Max(1.0, DroughtSlope * rate * secondsSinceProc - DroughtOffset);
        double chance = drought * secondsSinceRoll / 60.0 * rate;
        if (!_procced) chance = Math.Max(chance, rate / FirstProcFloorDivisor);

        bool procced = ctx.Outcomes.RollProc(ProcKey, Math.Clamp(chance, 0.0, 1.0), ctx, target);
        if (procced)
        {
            _lastProc = now;
            _procced = true;
        }
        return procced;
    }
}
