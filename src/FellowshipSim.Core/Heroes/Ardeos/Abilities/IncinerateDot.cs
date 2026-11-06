namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 1829: each Incinerate hit applies a stacking damage over time dealing Intellect fire damage on a period. The
/// damage over time portion is increased for each stack applied to an enemy. Combat logs (yMAZVJL9hTpFjqbC fight 7): the
/// damage grows linearly with the stacks, by <see cref="DamagePerExtraStack"/> of the base for each stack after the first.
/// </summary>
public sealed class IncinerateDot : DamageOverTime
{
    public const int EffectId = 1829;
    public const string EffectName = "Incinerate";
    public const double Coefficient = 0.3895;
    public const double PeriodSeconds = 3.0;
    public const double DurationSeconds = 12.0;
    public const double DamagePerExtraStack = 0.30;
    /// <summary>Effect 1829 maxStacks 50.</summary>
    public const int StackLimit = 50;

    public IncinerateDot(Ardeos owner) : base(owner, EffectId, EffectName, PeriodSeconds, DurationSeconds, DotStacking.Stacking)
    {
        BaseCoefficient = Coefficient;
        MaxStacks = StackLimit;
    }

    public override double StackMultiplier(DotInstance dot) => 1.0 + DamagePerExtraStack * (dot.Stacks - 1);
}
