namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// What a decision of <see cref="AplProgram"/> reads, step by step. A program runs it as a struct type argument, so the
/// sim's decisions (<see cref="NoDecisionTrace"/>) compile to no calls.
/// </summary>
internal interface IAplDecisionTrace
{
    /// <summary>A <c>variable</c> action before it runs; <paramref name="holds"/> is its condition.</summary>
    void Variable(BoundAplVariable step, SimulationContext ctx, bool holds);

    /// <summary>The <c>variable</c> action just traced has set its variable.</summary>
    void VariableSet(BoundAplVariable step, SimulationContext ctx);

    /// <summary>A list action and its outcome: <see cref="AplStepOutcome.ListEntered"/>, <see cref="AplStepOutcome.ListRunning"/> or <see cref="AplStepOutcome.ConditionFalse"/>.</summary>
    void ListAction(BoundAplListAction step, SimulationContext ctx, AplStepOutcome outcome);

    /// <summary>The list entered last has ended.</summary>
    void ListEnded();

    /// <summary>A <c>switch_target</c> action before it runs.</summary>
    void SwitchTarget(BoundAplSwitchTarget step, SimulationContext ctx);

    /// <summary>An ability action before its cast; <paramref name="canExecute"/> is <see cref="BoundAplAction.CanExecute"/>.</summary>
    void Action(BoundAplAction step, SimulationContext ctx, bool canExecute);

    /// <summary>The cast of the action just traced: started, or refused with its reason.</summary>
    void Cast(CastAttempt attempt);
}
