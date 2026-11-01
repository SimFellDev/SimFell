namespace FellowshipSim.Core.APL.Debugging;

/// <summary>An enemy in combat: its health, whether it is the hero's current target, and the hero's debuffs on it.</summary>
public sealed record AplEnemyValue(string Name, double HealthPercent, bool Current, IReadOnlyList<AplAuraValue> Debuffs);
