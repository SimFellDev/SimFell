using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// A base class for the test decorators that change a fight's random outcomes. It forwards every call to the wrapped outcome source (<see
/// cref="Inner"/>); a subclass overrides only the calls it controls.
///
/// The engine never rolls dice itself: every random outcome (procs, crits, travel times, damage spread, targets) is a call on <see
/// cref="SimulationContext.Outcomes"/>. A sim sets it to <see cref="RandomOutcomeSource"/>, a replay to the combat log's answers. A test
/// replaces it with a decorator around the current source, which changes one fight's outcomes through the engine's own API, with no engine
/// code changed.
///
/// Decorators stack, the last one installed outermost: <c>NoDamageSpread(ForcedCrits(ForcedProcs(RandomOutcomeSource)))</c>. Tests install
/// them through <see cref="StepForcing"/> and never build one directly.
/// </summary>
public abstract class OutcomeSourceDecorator(IOutcomeSource inner) : IOutcomeSource
{
    protected IOutcomeSource Inner { get; } = inner;

    public virtual bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null) =>
        Inner.RollProc(procKey, chance, ctx, target);

    public virtual IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) =>
        Inner.SelectTargets(damageLogId, candidates, ctx);

    public virtual EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) =>
        Inner.ChooseTarget(damageLogId, candidates, ctx, preferred);

    public virtual HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => Inner.ResolveHit(hit, ctx);

    public virtual double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx) => Inner.Land(landed, outcome, ctx);

    public virtual SimTime TickAt(in TickRequest tick, SimulationContext ctx) => Inner.TickAt(tick, ctx);

    public virtual void HitFinished(SimulationContext ctx) => Inner.HitFinished(ctx);

    public virtual SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => Inner.ReactionDelay(hold, reactions, ctx);

    public virtual double LoggedHitAmount(int damageLogId, EnemyTarget target, SimulationContext ctx) => Inner.LoggedHitAmount(damageLogId, target, ctx);

    public virtual double RollDuration(in EffectDurationRequest application, SimulationContext ctx) => Inner.RollDuration(application, ctx);

    public virtual double RollDelay(double maxSeconds, SimulationContext ctx) => Inner.RollDelay(maxSeconds, ctx);
}
