namespace FellowshipSim.Core.Domain;

/// <summary>
/// Effect 2122, Urrak Markets: a Slavebind Totem's death (ability 293) gives every hero within
/// range Breaker of Chains: a multiplicative CooldownRecovery bonus for a duration, both literals. A second totem refreshes it.
/// The engine has no positions, so every totem's death reaches the hero.
/// </summary>
public static class BreakerOfChains
{
    public const int EffectId = 2122;
    public const string CreatureName = "Slavebind Totem";
    public const double BuffSeconds = 12.0;
    public const double CooldownRecovery = 1.3;

    public static readonly AuraDef Buff = AuraDef.Effect("Breaker of Chains", EffectId, BuffSeconds,
        modifiers: StatModifier.Increased(StatType.CooldownRecovery, CooldownRecovery));
}
