namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>Stats every legendary item carries, whatever its power.</summary>
public static class LegendaryStats
{
    public const int StrandOfEternityEffectId = 3087;
    /// <summary>Cooldown recovery bonus.</summary>
    public const double StrandOfEternityRecovery = 1.1;
    public const int MoteOfCreationEffectId = 3085;
    /// <summary>
    /// Primary stat bonus: effect 3085, PrimaryAttributeMultiplier Additive. The attribute sums its Additive modifiers
    /// before it multiplies, so Mote of Creation and Rising Spirit (effect 3115, Additive per stack) add to the bonus.
    /// Combat logs (Rime's matched hits in the 15 fights of docs/accuracy/rime-accuracy-v1.md but NhBnFXmy3bJg9Cav #32 and
    /// #33, the engine multiplying the two): log/engine under Rising Spirit over log/engine without it falls below 1 as
    /// stacks rise (24,426 hits at 1 stack), as the sum over the product gives.
    /// </summary>
    public const double MoteOfCreationPrimaryStat = 0.1;

    public static readonly AuraDef StrandOfEternity = AuraDef.Effect("Strand of Eternity", StrandOfEternityEffectId,
        modifiers: StatModifier.Increased(StatType.CooldownRecovery, StrandOfEternityRecovery));

    public static readonly AuraDef MoteOfCreation = AuraDef.Effect("Mote of Creation", MoteOfCreationEffectId,
        modifiers: StatModifier.Add(StatType.PrimaryAttributeMultiplier, MoteOfCreationPrimaryStat));
}
