namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 2293: Fire Ball sets the enemies it hits on fire, dealing an additional share of the initial damage over its
/// duration. Fire Ball's damage over time can not critically strike. A new Fire Ball adds its share to the damage not yet
/// dealt.
/// The tooltip gives each tick a chance to generate Cinders: combat logs fit every tick generating them, scaled by the
/// target falloff of the Fire Ball that applied it (yMAZVJL9hTpFjqbC fight 7). Replays of the five logged fights refuse
/// the fewest Detonates for want of Cinders with every tick generating them, more with a share of them or none.
/// </summary>
public sealed class FireBallDot : DamageOverTime
{
    public const int EffectId = 2293;
    public const string EffectName = "Fire Ball";
    public const double PeriodSeconds = 2.0;
    public const double DurationSeconds = 12.0;
    public const double CindersPerTickGenerated = 2.0;

    public FireBallDot(Ardeos owner) : base(owner, EffectId, EffectName, PeriodSeconds, DurationSeconds, DotStacking.Accumulate)
    {
        TicksCanCrit = false;
        CindersPerTick = CindersPerTickGenerated;
    }
}
