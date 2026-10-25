using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A save refused for the list's name, or for the fields that do not parse (<see cref="Errors"/>).</summary>
public sealed class ActionListNotSavedException(string message, IReadOnlyList<AplDocumentError> errors) : Exception(message)
{
    public IReadOnlyList<AplDocumentError> Errors { get; } = errors;
}
