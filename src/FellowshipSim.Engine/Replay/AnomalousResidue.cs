using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Dungeons.Affixes;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Anomalous Residue (<see cref="AnomalousOrbs"/>): each stack adds <see cref="PowerPerStack"/> to one colour's gem power
/// for a limited duration and number of stacks (effects 2704-2710, base effect 2709). A replay follows the log's stacks
/// on the player and changes the gem's power with them (<see cref="AnomalousOrbs.SetResidue"/>).
/// </summary>
public static class AnomalousResidue
{
    public const double PowerPerStack = AnomalousOrbs.Constants.AnomalousResidueAdditionalGemPowerPerStack;

    /// <summary>The colour each Residue effect raises, by log ID (effects 2704-2708 and 2710).</summary>
    public static readonly IReadOnlyDictionary<int, GemColor> ColourByLogId =
        AnomalousOrbs.Residue.ToDictionary(r => r.Value.LogId, r => r.Key);
}
