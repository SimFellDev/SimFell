using FellowshipSim.Core.Engine;

namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// Every decision of one fight (<see cref="MonteCarloSimulator.TraceFight"/>), in order, with each action line the
/// decision read. <see cref="Lines"/> are the action lists' lines in list order; a step names its line by its index in
/// <see cref="Lines"/>. <see cref="Casts"/> are the fight's casts as the sim's typical fight lists them.
/// </summary>
public sealed record AplFightTrace(
    string Hero,
    double FightSeconds,
    IReadOnlyList<AplTraceLine> Lines,
    IReadOnlyList<AplDecision> Decisions,
    IReadOnlyList<CastReport> Casts);
