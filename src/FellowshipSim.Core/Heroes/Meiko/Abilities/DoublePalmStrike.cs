namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1105, a Palm Technique finisher (Spirit Palm then Wind Kick): "Strike target enemy with both palms, dealing
/// 1253.2% of Agility damage. Grants Spirited Strikes for 30 seconds. SPIRITED STRIKES: +25% Magic Damage Reduction, +5%
/// Parry. Can be used during Global Cooldown". Logged under 1105. Magic damage reduction and parry are not simulated.
/// </summary>
public class DoublePalmStrike : MeikoFinisher
{
    public const int AbilityId = 1105;
    public const string AbilityName = "Double Palm Strike";
    /// <summary>Placeholder DoublePalmDmg: 1253.2% of Agility.</summary>
    public const double Coefficient = 12.532;
    /// <summary>Effect 1534: MagicalIncomingDamageMultiplier x0.75 and ParryChance +0.05 for 30 seconds.</summary>
    public const int SpiritedStrikesEffectId = 1534;
    public const double SpiritedStrikesSeconds = 30.0;

    public static readonly AuraDef SpiritedStrikes = AuraDef.Effect("Spirited Strikes", SpiritedStrikesEffectId, SpiritedStrikesSeconds);

    /// <summary>A Double Palm Strike struck its enemy (Forbidden Technique).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public DoublePalmStrike(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core, Technique.Palm, Technique.Kick)
    {
        BaseCoefficient = Coefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        DealDamage(ctx, target);
        Owner.Auras.Apply(SpiritedStrikes, ctx.CurrentTime);
        Struck?.Invoke(ctx, target);
    }
}
