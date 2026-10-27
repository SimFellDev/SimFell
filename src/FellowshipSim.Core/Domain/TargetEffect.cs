namespace FellowshipSim.Core.Domain;

/// <summary>
/// An aura or an effect on an enemy as an action list reads it (<c>buff.</c>, <c>debuff.</c>, <c>dot.</c>): when it runs
/// out, its stacks and their limit, and its full duration.
/// </summary>
public readonly record struct TargetEffect(SimTime ExpiresAt, int Stacks, int MaxStacks, SimTime Duration)
{
    public static TargetEffect Of(Aura aura) => new(aura.ExpiresAt, aura.Stacks, aura.MaxStacks, aura.Duration);
}
