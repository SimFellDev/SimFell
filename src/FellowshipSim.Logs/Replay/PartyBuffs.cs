using FellowshipSim.Core.Customization.Traits;

namespace FellowshipSim.Logs.Replay;

/// <summary>
/// Buffs other players or the dungeon put on the replayed player that change the player's damage or cooldowns. In a
/// replay, a logged buff from another player is applied with the effect listed here (one instance per giver; the log
/// decides when it ends). Logged buffs not listed here are reported as unmodeled in the review: add them here when they
/// matter.
/// </summary>
public static class PartyBuffs
{
    /// <summary>
    /// A buff from another player. When it comes from one of the giver's traits, <see cref="ValueByGiverRank"/> holds its
    /// modifier value at each trait rank, and the giver's rank (gear trait ranks + weapon-tree points) picks the value.
    /// </summary>
    public sealed record PartyBuff(AuraDef Aura, IReadOnlyList<double>? ValueByGiverRank = null, string? TraitName = null, int TraitId = 0);

    public const int ImbuedBloodEffectId = 55;
    public const int ShadowsDefeatEffectId = 2221;
    public const int BreakerOfChainsEffectId = 2122;

    public static readonly PartyBuff[] All =
    [
        // Inspired Allegiance (trait 2): Haste rating for the giver and allies, by the giver's rank.
        new(AuraDef.Effect(InspiredAllegiance.TraitName, InspiredAllegiance.BuffEffectId,
                modifiers: StatModifier.Add(StatType.HasteRating, InspiredAllegiance.HasteRatingByRank[0])),
            InspiredAllegiance.HasteRatingByRank, InspiredAllegiance.TraitName, InspiredAllegiance.TraitId),

        // Imbued Blood: +1% Expertise per stack, up to 15 stacks.
        new(AuraDef.Effect("Imbued Blood", ImbuedBloodEffectId, maxStacks: 15, modifiers: StatModifier.Add(StatType.Expertise, 0.01))),

        // Shadow's Defeat: 20% more damage and 20% increased Haste for 60 seconds.
        new(AuraDef.Effect("Shadow's Defeat", ShadowsDefeatEffectId, 60,
            modifiers: [StatModifier.More(StatType.DamageScale, 1.2), StatModifier.Increased(StatType.Haste, 1.2)])),

        // Breaker of Chains (Urrak Markets, every player near a Slavebind Totem as it dies): CooldownRecovery x1.3 for 12
        // seconds (game data GE_UM_CageDeath_Buff, op Multiplicitive, so it joins the recovery pool as +0.3). Weapon
        // cooldowns ignore it. The log shows it as the player's own buff.
        new(AuraDef.Effect("Breaker of Chains", BreakerOfChainsEffectId, 12,
            modifiers: StatModifier.Increased(StatType.CooldownRecovery, 1.3))),
    ];

    public static PartyBuff? Find(int logId) => All.FirstOrDefault(b => b.Aura.LogId == logId);
}
