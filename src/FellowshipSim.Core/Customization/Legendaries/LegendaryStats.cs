namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>Stats every legendary item carries, whatever its power.</summary>
public static class LegendaryStats
{
    public const int StrandOfEternityEffectId = 3087;
    /// <summary>+10% cooldown recovery.</summary>
    public const double StrandOfEternityRecovery = 1.1;
    public const int MoteOfCreationEffectId = 3085;
    /// <summary>
    /// +10% primary stat: effect 3085, PrimaryAttributeMultiplier Additive 0.1. The attribute sums its Additive modifiers
    /// before it multiplies (game-data-binary, exe 0x144AA79C0), so Mote of Creation and Rising Spirit (effect 3115,
    /// Additive 0.05 per stack) give 1.1 + 0.05 n. Combat logs (Rime's matched hits in the 15 fights of
    /// docs/accuracy/rime-accuracy-v1.md but NhBnFXmy3bJg9Cav #32 and #33, the engine multiplying the two): log/engine
    /// under Rising Spirit over log/engine without it is 0.995 at 1 stack (24,426 hits), 0.992 at 2 and 0.982 at 5, as
    /// (1.1 + 0.05 n) / (1.1 x (1 + 0.05 n)) gives.
    /// </summary>
    public const double MoteOfCreationPrimaryStat = 0.1;

    public static readonly AuraDef StrandOfEternity = AuraDef.Effect("Strand of Eternity", StrandOfEternityEffectId,
        modifiers: StatModifier.Increased(StatType.CooldownRecovery, StrandOfEternityRecovery));

    public static readonly AuraDef MoteOfCreation = AuraDef.Effect("Mote of Creation", MoteOfCreationEffectId,
        modifiers: StatModifier.Add(StatType.PrimaryAttributeMultiplier, MoteOfCreationPrimaryStat));
}
