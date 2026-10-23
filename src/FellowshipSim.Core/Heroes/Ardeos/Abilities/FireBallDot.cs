namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 2293: Fire Ball sets the enemies it hits "on fire, dealing an additional 20% of the initial damage dealt over 12
/// seconds" (ticks every 2s). "Fire Ball's damage over time can not critically strike." A new Fire Ball adds its share to
/// the damage not yet dealt.
/// "Each tick of Fireball has a 50% chance to generate 2 Cinders": combat logs fit every tick generating them, scaled by
/// the target falloff of the Fire Ball that applied it (deltas of 1.17-1.43 Cinders, 2 x sqrt(5/N), in
/// yMAZVJL9hTpFjqbC fight 7). Replays of the five logged fights refuse the fewest Detonates for want of Cinders with every
/// tick generating them (84-272 forced casts), more with half of them (87-292) or none (88-310).
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
