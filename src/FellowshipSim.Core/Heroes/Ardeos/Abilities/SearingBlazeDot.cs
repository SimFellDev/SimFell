namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 1820: "Target enemy burns for 64.695% of Intellect fire damage every 2 seconds for 30 seconds. Generates 12
/// Cinders over its full duration." Applying it again starts its 30 seconds again.
/// </summary>
public sealed class SearingBlazeDot : DamageOverTime
{
    public const int EffectId = 1820;
    public const string EffectName = "Searing Blaze";
    public const double Coefficient = 0.64695;
    public const double PeriodSeconds = 2.0;
    public const double DurationSeconds = 30.0;
    /// <summary>"Generates 12 Cinders over its full duration": 15 ticks of 0.8.</summary>
    public const double CindersOverDuration = 12.0;

    public SearingBlazeDot(Ardeos owner) : base(owner, EffectId, EffectName, PeriodSeconds, DurationSeconds, DotStacking.Refresh)
    {
        BaseCoefficient = Coefficient;
        CindersPerTick = CindersOverDuration * PeriodSeconds / DurationSeconds;
    }

    /// <summary>Damage added per tick dealt so far, and the most ticks that count (Agonizing Blaze).</summary>
    public double DamagePerTickDealt { get; set; }
    public int MaxTicksCounted { get; set; }

    public override double StackMultiplier(DotInstance dot) => 1.0 + DamagePerTickDealt * Math.Min(dot.Ticks, MaxTicksCounted);
}
