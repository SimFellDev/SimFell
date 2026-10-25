namespace FellowshipSim.Core.Engine;

/// <summary>
/// One buff on the hero, or debuff on the targets (<see cref="OnTarget"/>), over a sim's fights. <see cref="Uptime"/> and
/// <see cref="AtMaxStacks"/> are shares of the fight (a debuff's, per target); <see cref="Applications"/> (each time it
/// starts) and <see cref="Refreshes"/> (each time it is applied again while up) are means per fight;
/// <see cref="AverageDuration"/> is how long each lasts, <see cref="AverageStacks"/> its stacks while up, and
/// <see cref="AverageGap"/> the time from one ending to the next starting, all in seconds. <see cref="LogId"/> is the
/// aura's log ID (an effect's + 1,000,000; 0 for an engine-internal aura). <see cref="Spans"/> are the times it was up in
/// one typical fight (the sampled fight nearest the mean damage), as start and end seconds in turn; a debuff's across
/// all targets.
/// </summary>
public sealed record BuffReport(
    string Name,
    bool OnTarget,
    double Uptime,
    double Applications,
    double Refreshes,
    double AverageDuration,
    double AverageStacks,
    int MaxStacks,
    double AtMaxStacks,
    double AverageGap,
    int LogId = 0,
    IReadOnlyList<double>? Spans = null);
