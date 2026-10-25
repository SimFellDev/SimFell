namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Effect 1829: each Incinerate hit "applies a stacking damage over time to them dealing 38.95% of Intellect fire damage
/// every 3 seconds for 12 seconds. The damage over time portion is increased by +30% for each stack that is applied to an
/// enemy." Combat logs (yMAZVJL9hTpFjqbC fight 7): a tick at 8 stacks is 3.1 times one at 1 stack, so stack n deals
/// 1 + 0.3 (n - 1) times the base.
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
