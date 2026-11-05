namespace FellowshipSim.Core.Engine;

/// <summary>What held the hero last before they could cast on the global cooldown again.</summary>
public enum ReactionHold
{
    /// <summary>The global cooldown ran out.</summary>
    GlobalCooldown,
    /// <summary>A cast with a cast time completed.</summary>
    CastTime,
    /// <summary>A channel ended.</summary>
    Channel,
}
