using System.Globalization;
using FellowshipSim.Core.APL.Debugging;

namespace FellowshipSim.UI.Debugger;

/// <summary>Values of a traced fight as the debugger writes them.</summary>
public static class AplValues
{
    /// <summary>A number with up to three decimals.</summary>
    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Seconds with three decimals.</summary>
    public static string Seconds(double seconds) => seconds.ToString("0.000", CultureInfo.InvariantCulture) + " s";

    /// <summary>An expression's value: true or false for a truth, else the number.</summary>
    public static string Of(AplNodeTrace node) => node.Truth ? (node.Value != 0 ? "true" : "false") : Number(node.Value);

    /// <summary>The outcome's label.</summary>
    public static string Outcome(AplStepTrace step) => step.Outcome switch
    {
        AplStepOutcome.Cast => "Cast",
        AplStepOutcome.ConditionFalse => "Condition false",
        AplStepOutcome.NoTarget => "No target",
        AplStepOutcome.NotReady => "Not ready",
        AplStepOutcome.CastRefused => "Cast refused",
        AplStepOutcome.VariableSet => $"Set to {Number(step.Value ?? 0)}",
        AplStepOutcome.VariableKept => $"Kept {Number(step.Value ?? 0)}",
        AplStepOutcome.ListEntered => "Entered",
        AplStepOutcome.ListRunning => "Already running",
        _ => step.Outcome.ToString(),
    };
}
