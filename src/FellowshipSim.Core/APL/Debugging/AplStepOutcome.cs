using System.Text.Json.Serialization;

namespace FellowshipSim.Core.APL.Debugging;

/// <summary>The outcome of one action line in a decision.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AplStepOutcome>))]
public enum AplStepOutcome
{
    /// <summary>The ability was cast. The decision ends here.</summary>
    Cast,
    /// <summary>The condition was false at the target.</summary>
    ConditionFalse,
    /// <summary><c>target_if</c> found no enemy.</summary>
    NoTarget,
    /// <summary>The condition held and the ability could not be cast: a cooldown, a resource or a cast already running.</summary>
    NotReady,
    /// <summary>The ability could be cast and the cast was refused.</summary>
    CastRefused,
    /// <summary>A <c>variable</c> action set its variable.</summary>
    VariableSet,
    /// <summary>A <c>variable</c> action's condition was false, so the variable kept its value.</summary>
    VariableKept,
    /// <summary>A list action ran its list. The list's steps follow at one more depth.</summary>
    ListEntered,
    /// <summary>A list action named a list running in this decision, so it was skipped.</summary>
    ListRunning,
}
