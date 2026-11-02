namespace FellowshipSim.Core.Domain;

/// <summary>
/// A proc of a block, parry, dodge, hit taken or heal (<see cref="Hero.AddDefensiveProc"/>). A sim has no incoming attacks
/// or heals, so it never fires. A replay fires it at each proof of <see cref="ProcKey"/> in the log, with the enemy of the
/// logged hit, or with the hero's current target for a proof that is an aura on the hero.
/// </summary>
/// <param name="ProcKey">The proc's name, with its proof in the replay's proc evidence table.</param>
/// <param name="Fire">
/// What the proc does: a hit (Xavian's Solaris, <see cref="Hero.DealDefensiveProcHit"/>), or an effect without damage
/// (Helena's Second Wind: 20% more Strength once her Toughness has reduced enough damage).
/// </param>
public sealed record DefensiveProc(string ProcKey, Action<SimulationContext, EnemyTarget?> Fire);
