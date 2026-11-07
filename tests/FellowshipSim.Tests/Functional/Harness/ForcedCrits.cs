using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// A decorator to explicitly control when crits occur during a test. Tests use it through <see cref="StepForcing"/>:
/// <c>f.Cast&lt;FrostBolt&gt;(crit: true)</c> makes every hit of that cast crit; <c>crit: false</c> makes none crit.
///
/// It calls the wrapped <see cref="ResolveHit"/> first, so the dice roll as usual, then overwrites <see cref="HitOutcome.IsCrit"/>. It
/// leaves alone missed hits, hits that cannot crit, and crits the engine forces itself (<see cref="HitRequest.ForcedCrit"/>).
///
/// A cast's hits can land after <c>Cast</c> returns (a falling comet, a channel's ticks), so a cast's override is stored by ability name:
/// <see cref="SetNext"/> arms it, the hero's <c>OnCastStarted</c> for that ability activates it, and it stays active until that ability's
/// next cast.
/// </summary>
public sealed class ForcedCrits : OutcomeSourceDecorator
{
    private readonly Dictionary<string, bool> _byAbility = new();
    private (string Ability, bool Crit)? _next;

    public ForcedCrits(IOutcomeSource inner, Hero hero) : base(inner)
    {
        hero.OnCastStarted += (ability, _, _) =>
        {
            _byAbility.Remove(ability.Name);
            if (_next is { } next && next.Ability == ability.Name) _byAbility[ability.Name] = next.Crit;
        };
    }

    /// <summary>Arms an override for the next cast of <paramref name="ability"/>.</summary>
    public void SetNext(Ability ability, bool crit) => _next = (ability.Name, crit);

    public void ClearNext() => _next = null;

    public override HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx)
    {
        var outcome = Inner.ResolveHit(hit, ctx);
        if (outcome.Missed || !hit.CanCrit || hit.ForcedCrit.HasValue) return outcome;
        return _byAbility.TryGetValue(hit.AbilityName, out bool crit) ? outcome with { IsCrit = crit } : outcome;
    }
}
