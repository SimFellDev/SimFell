namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// The hero and the enemies at a decision: resources, buffs, the global cooldown's seconds left (0 when it has ended),
/// cooldowns, variables and each enemy in combat.
/// </summary>
public sealed record AplHeroState(
    IReadOnlyList<AplResourceValue> Resources,
    IReadOnlyList<AplAuraValue> Buffs,
    double GlobalCooldown,
    IReadOnlyList<AplCooldownValue> Cooldowns,
    IReadOnlyList<AplVariableValue> Variables,
    IReadOnlyList<AplEnemyValue> Enemies);
