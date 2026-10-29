using FellowshipSim.Engine;

namespace FellowshipSim.UI.Services;

/// <summary>
/// A finished sim as saved in the data folder: its job's ID and title, the profile it ran, its settings and its DPS. A
/// compare or finder sim also has <see cref="Runs"/>, the first of them the baseline; <see cref="Profile"/> and
/// <see cref="Result"/> are then that first run's.
/// </summary>
public sealed record SavedSim(
    string Id,
    string Title,
    string Hero,
    string ProfileName,
    string Profile,
    int Targets,
    SimResult Result,
    DateTime CreatedAt,
    IReadOnlyList<SimRun>? Runs = null);

/// <summary>
/// One run of a compare or finder sim: what it ran (<see cref="Label"/>, then <see cref="Detail"/>), its hero, its action
/// list (by name, and as the Sim page's picker keeps it), the profile and its result.
/// </summary>
public sealed record SimRun(
    string Label,
    string Detail,
    string Hero,
    string ActionListName,
    string ActionListValue,
    string Profile,
    SimResult Result);
