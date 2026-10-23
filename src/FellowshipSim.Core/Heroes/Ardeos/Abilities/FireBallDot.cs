namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 2293: Fire Ball sets the enemies it hits "on fire, dealing an additional 20% of the initial damage dealt over 12
/// seconds" (ticks every 2s). "Fire Ball's damage over time can not critically strike." "Each tick of Fireball has a 50%
/// chance to generate 2 Cinders." A new Fire Ball adds its share to the damage not yet dealt.
/// </summary>
public sealed class FireBallDot : DamageOverTime
{
    public const int EffectId = 2293;
    public const string EffectName = "Fire Ball";
    public const double PeriodSeconds = 2.0;
    public const double DurationSeconds = 12.0;
    public const double CindersPerTickProc = 2.0;
    public const double CinderChance = 0.5;
    /// <summary>The proc of a tick's Cinders. UNVERIFIED: the log shows Cinders, not which tick gave them.</summary>
    public const string CinderProcKey = "fire_ball_cinders";

    public FireBallDot(Ardeos owner) : base(owner, EffectId, EffectName, PeriodSeconds, DurationSeconds, DotStacking.Accumulate)
    {
        TicksCanCrit = false;
        CindersPerTick = CindersPerTickProc;
    }

    protected override double TickCinders(SimulationContext ctx, DotInstance dot, double fraction) =>
        ctx.Outcomes.RollProc(CinderProcKey, CinderChance, ctx, dot.Target) ? base.TickCinders(ctx, dot, fraction) : 0.0;
}
