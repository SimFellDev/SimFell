namespace FellowshipSim.Core.APL.Debugging;

/// <summary>The trace of a debugged decision: each call goes to the program's <see cref="AplDecisionLog"/>.</summary>
internal readonly struct LoggedDecisionTrace(AplDecisionLog log) : IAplDecisionTrace
{
    public void Variable(BoundAplVariable step, SimulationContext ctx, bool holds) => log.Variable(step, ctx, holds);
    public void VariableSet(BoundAplVariable step, SimulationContext ctx) => log.VariableSet(step, ctx);
    public void ListAction(BoundAplListAction step, SimulationContext ctx, AplStepOutcome outcome) => log.ListAction(step, ctx, outcome);
    public void ListEnded() => log.ListEnded();
    public void SwitchTarget(BoundAplSwitchTarget step, SimulationContext ctx) => log.SwitchTarget(step, ctx);
    public void Action(BoundAplAction step, SimulationContext ctx, bool canExecute) => log.Action(step, ctx, canExecute);
    public void Cast(CastAttempt attempt) => log.Cast(attempt);
}
