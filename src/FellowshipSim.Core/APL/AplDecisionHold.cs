namespace FellowshipSim.Core.APL;

/// <summary>
/// The hold on the hero as a decision runs. It sets the decision's candidate actions: every ability action with no hold,
/// <c>use_off_gcd=1</c> actions during <see cref="GlobalCooldown"/>, and <c>use_while_casting=1</c> actions of abilities
/// that ignore the global cooldown during <see cref="Cast"/>.
/// </summary>
public enum AplDecisionHold
{
    /// <summary>Nothing holds the hero.</summary>
    None,
    /// <summary>The global cooldown or the reaction after a hold runs, and no cast or channel.</summary>
    GlobalCooldown,
    /// <summary>A cast or a channel runs.</summary>
    Cast,
}
