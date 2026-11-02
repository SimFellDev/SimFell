namespace FellowshipSim.Core.Domain;

/// <summary>
/// A proc the log proves (<see cref="Hero.AddDefensiveProc"/>). It fires in a replay only: at each proof of
/// <see cref="ProcKey"/> in the log, with the proof's enemy, or with the hero's current target for a proof without one.
/// </summary>
/// <param name="ProcKey">The proc's name, with its proof in the replay's proc evidence table.</param>
/// <param name="Fire">
/// What the proc does: a hit (Xavian's Solaris, <see cref="Hero.DealDefensiveProcHit"/>), or an effect without damage
/// (Helena's Second Wind buff).
/// </param>
public sealed record DefensiveProc(string ProcKey, Action<SimulationContext, EnemyTarget?> Fire);
