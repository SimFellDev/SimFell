namespace FellowshipSim.Core.Engine;

public class RealPpm
{
    public double BasePpm { get; }
    public SimTime LastProcAttemptTime { get; private set; } = SimTime.Zero;

    public string ProcKey { get; }

    public RealPpm(string procKey, double basePpm)
    {
        ProcKey = procKey;
        BasePpm = basePpm;
    }

    /// <summary>The fight of the last attempt (each sim iteration and replay has its own). A new fight starts at time 0.</summary>
    private SimulationContext? _fight;

    public bool Roll(Hero hero, SimulationContext ctx, EnemyTarget? target = null)
    {
        if (_fight != ctx)
        {
            LastProcAttemptTime = SimTime.Zero;
            _fight = ctx;
        }
        double timeSinceLastSeconds = (ctx.CurrentTime - LastProcAttemptTime).TotalSeconds;
        LastProcAttemptTime = ctx.CurrentTime;

        // Haste scales RPPM proc frequency
        double hastedPpm = BasePpm * (1.0 + hero.GetHaste(ctx.CurrentTime));
        double procChance = Math.Clamp((hastedPpm * timeSinceLastSeconds) / 60.0, 0.0, 1.0);

        return ctx.Outcomes.RollProc(ProcKey, procChance, ctx, target);
    }
}

