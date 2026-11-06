namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 1826: a Fire Frog's attack burns the enemy for an additional share of the initial damage dealt over the
/// effect's duration. Fire Frogs' damage over time can not critically strike. Combat logs (yMAZVJL9hTpFjqbC fight 7): a new attack adds its damage to the damage not yet dealt.
/// </summary>
public sealed class FireFrogsDot : DamageOverTime
{
    public const int EffectId = 1826;
    public const string EffectName = "Fire Frogs";
    public const double PeriodSeconds = 3.0;
    public const double DurationSeconds = 12.0;

    public FireFrogsDot(Ardeos owner) : base(owner, EffectId, EffectName, PeriodSeconds, DurationSeconds, DotStacking.Accumulate)
    {
        TicksCanCrit = false;
    }
}
