namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 2325: deals Intellect fire damage on a period and generates Cinders over its full duration. Every application is an instance of its own.
/// </summary>
public sealed class EngulfingFlamesDot : DamageOverTime
{
    public const int EffectId = 2325;
    public const string EffectName = "Engulfing Flames";
    public const double Coefficient = 1.8886;
    public const double PeriodSeconds = 1.5;
    public const double DurationSeconds = 9.0;
    /// <summary>Cinders over the full duration, the same each tick (Undying Flame's longer duration adds ticks of the same Cinders).</summary>
    public const double CindersOverDuration = 30.0;

    public EngulfingFlamesDot(Ardeos owner) : base(owner, EffectId, EffectName, PeriodSeconds, DurationSeconds, DotStacking.Concurrent)
    {
        BaseCoefficient = Coefficient;
        CindersPerTick = CindersOverDuration * PeriodSeconds / DurationSeconds;
    }
}
