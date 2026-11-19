using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Buffs other players put on the replayed player that change the player's damage, and auras on enemies that change the
/// damage the enemy takes. In a replay, a logged aura from another actor is applied with the effect listed here (one
/// instance per giver; the log decides when it ends). Logged buffs on the player not listed here are reported as
/// unmodeled in the review: add them here when they matter.
/// </summary>
public static class PartyBuffs
{
    /// <summary>
    /// A buff from another player. When it comes from one of the giver's sigils, <see cref="ValueByGiverRank"/> holds its
    /// modifier value at each sigil rank, and the giver's rank (gear ranks + weapon-tree points) picks the value.
    /// </summary>
    public sealed record PartyBuff(AuraDef Aura, IReadOnlyList<double>? ValueByGiverRank = null, string? SigilName = null, int SigilId = 0);

    public const int ImbuedBloodEffectId = 55;
    public const int ShadowsDefeatEffectId = 2221;
    public const int AnomalousInstabilityEffectId = 2711;
    public const int GreedsEmbraceEffectId = 649;
    public const int BloodFrenzyEffectId = 192;
    public const int BloodScentEffectId = 3045;
    public const int GreedyEffectId = 194;
    public const int OrbEnergyEffectId = 2437;
    public const int RainOfBloodEffectId = 2896;
    public const int ReturnedBuffEffectId = 2463;
    public const int WardstoneVulnerableEffectId = 1112;
    public const int WardstoneFocusingVulnerableEffectId = 1108;
    public const int StormsFuryEffectId = 2699;

    public static readonly PartyBuff[] All =
    [
        // Inspired Allegiance (sigil 2): Haste rating for the giver and allies, by the giver's rank.
        new(AuraDef.Effect(InspiredAllegiance.SigilName, InspiredAllegiance.BuffEffectId,
                modifiers: StatModifier.Add(StatType.HasteRating, InspiredAllegiance.Constants.BuffAdditionalHasteRatingByRank[0])),
            InspiredAllegiance.Constants.BuffAdditionalHasteRatingByRank, InspiredAllegiance.SigilName, InspiredAllegiance.SigilId),

        // Imbued Blood: Expertise per stack.
        new(AuraDef.Effect("Imbued Blood", ImbuedBloodEffectId, maxStacks: 15, modifiers: StatModifier.Add(StatType.Expertise, 0.01))),

        // Shadow's Defeat: more damage and increased Haste.
        new(AuraDef.Effect("Shadow's Defeat", ShadowsDefeatEffectId, 60,
            modifiers: [StatModifier.More(StatType.DamageScale, 1.2), StatModifier.Increased(StatType.Haste, 1.2)])),

        // Greedy (Greedspawned affix): effect modifier DamageScale, Multiplicitive per stack.
        new(AuraDef.Effect("Greedy", GreedyEffectId, maxStacks: 10, modifiers: StatModifier.Increased(StatType.DamageScale, 1.02))),

        // Storm's Fury (Storm Shield affix 25, Storm Shield: Lightning Strike, ability 1946): Critical Strike chance
        // (effect modifier CritChance, Additive). Logs: the Environment gives it to every player of a Storm Shield
        // run (16 downloaded fights).
        new(AuraDef.Effect("Storm's Fury", StormsFuryEffectId, 20, modifiers: StatModifier.Add(StatType.CritChance, 0.2))),

        // Orb Energy: additional damage per stack (effect modifier DamageScale, Multiplicitive).
        new(AuraDef.Effect("Orb Energy", OrbEnergyEffectId, maxStacks: 99, modifiers: StatModifier.Increased(StatType.DamageScale, 1.05))),

        // Rain of Blood (Overlord Varux): damage dealt per stack (effect modifier DamageScale, Multiplicitive).
        new(AuraDef.Effect("Rain of Blood", RainOfBloodEffectId, maxStacks: 100, modifiers: StatModifier.Increased(StatType.DamageScale, 1.05))),

        // Returned Buff (Deathless Katrine's Sever Soul, ability 589): effect 2463 modifier DamageScale MultiplyCompound.
        // Logs: Rime's hits under it exceed the engine's without it (NhBnFXmy3bJg9Cav fight 33).
        new(AuraDef.Effect("Returned Buff", ReturnedBuffEffectId, modifiers: StatModifier.More(StatType.DamageScale, 1.4))),

        // Enemy auras: only their damage taken is listed.
        // Anomalous Instability (Anomalous Orb affix): increased damage taken. Logs: hits on targets with it exceed
        // the engine's (Fc8v6yWxD14GQMnV fight 19, pWmcV42K39GHTLQX fight 23).
        new(AuraDef.Effect("Anomalous Instability", AnomalousInstabilityEffectId,
            modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, 4.0))),

        // Vulnerable (Godfall Wardstone, stunned or interrupted through Brittle, ability 898; and Focusing, ability 896):
        // effect modifier IncomingDamageMultiplier, Multiplicitive. Logs: Rime's hits on Godfall Wardstones exceed
        // the engine's without it (GWChJ8P7tn6fHgV3 fight 20, AY1m6jPKTZyx4vz2 fight 9).
        new(AuraDef.Effect("Vulnerable", WardstoneVulnerableEffectId,
            modifiers: StatModifier.Increased(StatType.IncomingDamageMultiplier, 4.0))),
        new(AuraDef.Effect("Vulnerable", WardstoneFocusingVulnerableEffectId,
            modifiers: StatModifier.Increased(StatType.IncomingDamageMultiplier, 4.0))),

        // Greed's Embrace (Greedspawned Demon): reduced damage taken. Logs: hits fall below the engine's, and the log's
        // mitigated part of a hit is this 0.8 (pWmcV42K39GHTLQX fight 23 @ 41061874: amount 1,022, mitigated 4,087). The
        // replay script drops it from the comparison.
        new(AuraDef.Effect("Greed's Embrace", GreedsEmbraceEffectId,
            modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, 0.2))),

        // Blood Frenzy (Raging Pillager): effect modifier IncomingDamageMultiplier, increased per stack. UNVERIFIED.
        new(AuraDef.Effect("Blood Frenzy", BloodFrenzyEffectId, maxStacks: 100,
            modifiers: StatModifier.Increased(StatType.IncomingDamageMultiplier, 1.03))),

        // Blood Scent (Stalking Grim): effect modifier IncomingDamageMultiplier, increased per stack. Logs: hits exceed the
        // engine's more with more stacks (dBgbn8wD1RxL7Kma fight 9).
        new(AuraDef.Effect("Blood Scent", BloodScentEffectId, maxStacks: 100,
            modifiers: StatModifier.Increased(StatType.IncomingDamageMultiplier, 1.1))),
    ];

    public static PartyBuff? Find(int logId) => All.FirstOrDefault(b => b.Aura.LogId == logId);
}
