namespace FellowshipSim.Core.Engine;

/// <summary>
/// Game attributes, named as in the game data (effect "modifiers[].attribute").
/// </summary>
public enum StatType
{
    PrimaryAttribute,
    PrimaryAttributeMultiplier,
    Stamina,
    CritRating,
    CritChance,
    CritMultiplier,
    HasteRating,
    Haste,
    ExpertiseRating,
    Expertise,
    SpiritRating,
    Spirit,
    DamageScale,
    CooldownReduction,
    CooldownRecovery,
    IncomingDamageMultiplier,
}

/// <summary>Modifier operations, as the game data names them (effect "modifiers[].op").</summary>
public enum ModifierOp
{
    /// <summary>Adds the value ("+X").</summary>
    Additive,
    /// <summary>"X increased": all such modifiers add their (value - 1) together, then multiply once (datamine op "Multiplicitive").</summary>
    MultiplyAdditive,
    /// <summary>"X more": each multiplies the result (datamine op "MultiplyCompound").</summary>
    MultiplyCompound,
}

/// <summary>One modifier, per stack of the aura carrying it.</summary>
public readonly record struct StatModifier(StatType Type, double Value, ModifierOp Op = ModifierOp.Additive)
{
    public static StatModifier Add(StatType type, double value) => new(type, value, ModifierOp.Additive);
    public static StatModifier Increased(StatType type, double multiplier) => new(type, multiplier, ModifierOp.MultiplyAdditive);
    public static StatModifier More(StatType type, double multiplier) => new(type, multiplier, ModifierOp.MultiplyCompound);
}

/// <summary>Accumulates modifiers for one attribute and applies them to a base value.</summary>
public struct ModifierFold
{
    private double _add;
    private double _increased;
    private double _more;
    private bool _hasMore;

    public void Accept(in StatModifier mod, int stacks)
    {
        switch (mod.Op)
        {
            case ModifierOp.Additive:
                _add += mod.Value * stacks;
                break;
            case ModifierOp.MultiplyAdditive:
                _increased += (mod.Value - 1.0) * stacks;
                break;
            case ModifierOp.MultiplyCompound:
                _more = (_hasMore ? _more : 1.0) * Math.Pow(mod.Value, stacks);
                _hasMore = true;
                break;
        }
    }

    public readonly double Apply(double baseValue) => (baseValue + _add) * (1.0 + _increased) * (_hasMore ? _more : 1.0);
}

public static class StatDiminishingReturns
{
    /// <summary>Percent per rating point (settings.json statCalculation.attributes.*.baseStatMultiplier = 0.16%).</summary>
    public const double PercentPerRating = 0.0016;

    private static readonly double[] Breakpoints = [0.10, 0.15, 0.20, 0.25];
    private static readonly double[] Multipliers = [
        1.0,
        0.98,
        0.96 * 0.98,
        0.94 * 0.96 * 0.98,
        0.92 * 0.94 * 0.96 * 0.98
    ];

    public static double Apply(double unmitigatedPercent)
    {
        if (unmitigatedPercent <= Breakpoints[0])
            return unmitigatedPercent * Multipliers[0];

        double output = Breakpoints[0] * Multipliers[0];
        double remaining = unmitigatedPercent - output;
        double currentBreakpoint = output;

        for (int i = 1; i < Breakpoints.Length; i++)
        {
            double prevBreakpoint = currentBreakpoint;
            currentBreakpoint += (Breakpoints[i] - Breakpoints[i - 1]) / Multipliers[i];
            double segment = currentBreakpoint - prevBreakpoint;

            if (remaining <= segment)
            {
                output += remaining * Multipliers[i];
                return output;
            }

            output += segment * Multipliers[i];
            remaining -= segment;
        }

        if (remaining > 0)
        {
            output += remaining * Multipliers[^1];
        }

        return output;
    }

    public static double FromRating(double rating) => Apply(rating * PercentPerRating);

    /// <summary>The rating whose percentage is <paramref name="percent"/>: the inverse of <see cref="FromRating"/>.</summary>
    public static double ToRating(double percent)
    {
        double unmitigated = Math.Min(percent, Breakpoints[0]) / Multipliers[0];
        double output = Breakpoints[0];
        int i = 1;
        for (; i < Breakpoints.Length && percent > output; i++)
        {
            double segment = Math.Min(percent, Breakpoints[i]) - output;
            unmitigated += segment / Multipliers[i];
            output += segment;
        }
        if (percent > output) unmitigated += (percent - output) / Multipliers[^1];
        return unmitigated / PercentPerRating;
    }

    public static double CalculateEffectiveExpertise(double rating, double flatBonus = 0.0) => FromRating(rating) + flatBonus;
}

/// <summary>
/// A hero's base stats. Ratings, primary attribute and Stamina start from the gear's (with the hero's base) and every
/// bonus adds to them as it binds.
/// </summary>
public class StatBlock
{
    public const double BaseCritChance = 0.05;
    /// <summary>Every hero's base Main Stat and Stamina (game data hero baseAttributes: 100 each for all 12 heroes).</summary>
    public const double BasePrimaryStat = 100;
    public const double BaseStamina = 100;
    /// <summary>
    /// Every hero's base Spirit: none. (The 10% once assumed here was Oracle's Foresight II + Death's Grasp, which nearly
    /// every logged build has; Xue, with only the gem, matches at 6%.)
    /// </summary>
    public const double BaseSpirit = 0.0;
    public const double BaseCritMultiplier = 2.0;
    /// <summary>
    /// Maximum health per point of Stamina, for sims without a logged health value. UNVERIFIED: measured on one log
    /// (PcaMVTFbZzNLrfx3: 32,563 health with 581 Stamina).
    /// </summary>
    public const double HealthPerStamina = 56.0;

    /// <summary>The primary stat (Intellect, Strength or Agility): the gear's and the base, with the flat bonuses.</summary>
    public double PrimaryStat { get; set { field = value; Version++; } }
    public double Stamina { get; set; } = 635;
    /// <summary>Multiplier on <see cref="Stamina"/> (Titan's Blood, the gem power overcap).</summary>
    public double StaminaMultiplier { get; set; } = 1.0;
    public double Armor { get; set; } = 539;

    /// <summary>Stamina with its multipliers.</summary>
    public double TotalStamina => Stamina * StaminaMultiplier;

    /// <summary>Maximum health: the logged value when known, else estimated from Stamina.</summary>
    public double? LoggedMaxHealth { get; set; }
    public double MaxHealth => LoggedMaxHealth ?? TotalStamina * HealthPerStamina;

    public double CritRating { get; set { field = value; Version++; } }
    public double HasteRating { get; set { field = value; Version++; } }
    public double ExpertiseRating { get; set { field = value; Version++; } }
    public double SpiritRating { get; set { field = value; Version++; } }

    /// <summary>Permanent percent bonuses beyond ratings (gem nodes), added on top of the base percentages.</summary>
    public double BonusCritChance { get; set { field = value; Version++; } }
    public double BonusHaste { get; set { field = value; Version++; } }
    public double BonusExpertise { get; set { field = value; Version++; } }
    public double BonusSpirit { get; set { field = value; Version++; } }

    public double DerivedCritChance => BaseCritChance + StatDiminishingReturns.FromRating(CritRating);
    public double DerivedHaste => StatDiminishingReturns.FromRating(HasteRating);
    public double DerivedExpertise => StatDiminishingReturns.FromRating(ExpertiseRating);
    public double DerivedSpirit => BaseSpirit + StatDiminishingReturns.FromRating(SpiritRating);

    public double CritPercent => DerivedCritChance + BonusCritChance;
    public double HastePercent => DerivedHaste + BonusHaste;
    public double ExpertisePercent => DerivedExpertise + BonusExpertise;
    public double SpiritPercent => DerivedSpirit + BonusSpirit;

    public double CooldownReductionPct { get; set; }

    /// <summary>Changes whenever the primary stat, a rating or a bonus percentage is set.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Percentage for a rating stat after <paramref name="extraRating"/> more rating, keeping the base
    /// percentage and adding only the diminishing-returns-adjusted difference.
    /// </summary>
    public static double WithExtraRating(double basePercent, double baseRating, double extraRating) =>
        extraRating == 0
            ? basePercent
            : basePercent + StatDiminishingReturns.FromRating(baseRating + extraRating) - StatDiminishingReturns.FromRating(baseRating);
}
