namespace FellowshipSim.Core.Import;

/// <summary>A profile line that does not read: its number from 1, and the problem without the line.</summary>
public sealed class SimcLineException(string part, int line, string text, string problem)
    : FormatException($"{part} line {line} '{text}': {problem}.")
{
    public int Line { get; } = line;
    public string Problem { get; } = problem;
}
