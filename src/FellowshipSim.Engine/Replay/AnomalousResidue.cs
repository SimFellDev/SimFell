using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Anomalous Residue (Anomalous Orb dungeon modifier): each stack adds <see cref="PowerPerStack"/> to one colour's gem
/// power for a limited duration and number of stacks (effects 2704-2710,
/// base effect 2709). The hero's gem power multipliers scale it, as they multiply after the Additive sum (Covetous Greed,
/// effect 3271, MultiplyCompound). A replay follows the log's stacks on the player and changes
/// the gem's power with them.
/// </summary>
public static class AnomalousResidue
{
    public const double PowerPerStack = 480;

    /// <summary>The colour each Residue effect raises, by log ID (effects 2704-2708 and 2710).</summary>
    public static readonly IReadOnlyDictionary<int, GemColor> ColourByLogId = new Dictionary<int, GemColor>
    {
        [LogIds.Effect(2704)] = GemColor.Topaz,
        [LogIds.Effect(2705)] = GemColor.Sapphire,
        [LogIds.Effect(2706)] = GemColor.Ruby,
        [LogIds.Effect(2707)] = GemColor.Emerald,
        [LogIds.Effect(2708)] = GemColor.Diamond,
        [LogIds.Effect(2710)] = GemColor.Amethyst,
    };
}
