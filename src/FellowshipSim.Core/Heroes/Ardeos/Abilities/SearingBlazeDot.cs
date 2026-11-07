namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 1820: the target enemy burns for Intellect fire damage on a period, and the burn generates Cinders over its full
/// duration. Applying it again starts its duration again. The tooltip's Cinder total is outdated: the Cinders over the
/// full duration are <see cref="CindersOverDuration"/>.
/// </summary>
public sealed class SearingBlazeDot : DamageOverTime
{
    public const int EffectId = 1820;
    public const string EffectName = "Searing Blaze";
    public const double Coefficient = 0.64695;
    public const double PeriodSeconds = 2.0;
    public const double DurationSeconds = 30.0;
    /// <summary>Cinders over the full duration, the same each tick (the tooltip's total differs).</summary>
    public const double CindersOverDuration = 15.0;

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
