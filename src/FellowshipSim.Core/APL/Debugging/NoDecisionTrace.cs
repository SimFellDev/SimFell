namespace FellowshipSim.Core.APL.Debugging;

/// <summary>The trace of a sim's decision: it reads nothing.</summary>
internal readonly struct NoDecisionTrace : IAplDecisionTrace
{
    public void Variable(BoundAplVariable step, SimulationContext ctx, bool holds) { }
    public void VariableSet(BoundAplVariable step, SimulationContext ctx) { }
    public void ListAction(BoundAplListAction step, SimulationContext ctx, AplStepOutcome outcome) { }
    public void ListEnded() { }
    public void SwitchTarget(BoundAplSwitchTarget step, SimulationContext ctx) { }
    public void Action(BoundAplAction step, SimulationContext ctx, bool canExecute) { }
    public void Cast(CastAttempt attempt) { }
}
