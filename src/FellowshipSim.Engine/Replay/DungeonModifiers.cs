namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The dungeon modifiers of a logged run (its dungeonstart event's affixes) that change the replayed hero. The log has no
/// aura for them, so a replay applies them to the hero it builds. Modifiers not listed here change nothing the replay
/// models: their auras come through the log (<see cref="PartyBuffs"/>), or they only change the enemies.
/// </summary>
public static class DungeonModifiers
{
    /// <summary>
    /// Modifier 16, Ultimatum: bosses deal more damage and have more health, and heroes' ability cooldowns are reduced. The
    /// cooldown reduction adds to the hero's other cooldown reduction. Logs: in 12 replays of 6
    /// Ultimatum fights (4Ln6PQZCRzc7Wpta 72, c6QH41C2dYq7P3zb 13, Cf7npHNzRmALFJQy 1, GXFz3pZ94aQr2chV 3, J8NxpFaC7Vjq2mZc 7,
    /// pWmcV42K39GHTLQX 23), the replay's cooldown issues fall with the reduction added, and fall less when it multiplies
    /// with the hero's. A replay takes the enemies' health from the log, so the boss parts change nothing there.
    /// </summary>
    public const int UltimatumId = 16;
    public const double UltimatumCooldownReduction = 0.10;

    /// <summary>Applies the modifiers <paramref name="ids"/> to <paramref name="hero"/>.</summary>
    public static void Apply(Hero hero, IEnumerable<int> ids)
    {
        foreach (int id in ids)
        {
            if (id == UltimatumId) hero.Stats.CooldownReductionPct += UltimatumCooldownReduction;
        }
    }
}
