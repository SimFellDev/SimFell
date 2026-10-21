namespace FellowshipSim.Core.Customization;

/// <summary>
/// Dungeon modifiers that change the heroes. A fight's dungeonstart event lists its modifiers ("affixes") by their ids in
/// the game data tag table Modifiers.
/// </summary>
public static class DungeonModifiers
{
    /// <summary>Modifiers.Dungeon.Ultimatum: "Heroes' ability cooldowns are reduced by 10%."</summary>
    public const int UltimatumId = 16;

    /// <summary>
    /// GE_Ascension_BossBoost_HeroBuff (infinite, EffectLogic.System, so never logged): CooldownReduction +
    /// Ultimatum.BossBoost.HeroBuff.CooldownReductionIncrease. Every hero cooldown effect (GE_Shared_Cooldown_DefaultScale,
    /// its _NoCooldownRecovery and HasteScale forms) lasts GEC_CooldownReductionMagnitude = 1 - CooldownReduction.
    /// </summary>
    public const double UltimatumCooldownReduction = 0.1;

    /// <summary>Gives <paramref name="hero"/> the effects of the fight's dungeon modifiers.</summary>
    public static void Apply(Hero hero, IEnumerable<int> modifierIds)
    {
        if (modifierIds.Contains(UltimatumId)) hero.Stats.CooldownReductionPct += UltimatumCooldownReduction;
    }
}
