namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1716, from Widow's Bite used in Stealth: "Applies Seething Poison to the target, dealing 122.76% of Agility
/// poison damage every 2 seconds for 60 seconds. While Seething Poison is active on any enemy, it takes 20% more damage
/// from your Hemorrhaging Strike Bleed and you gain Predator's Rush, granting you 20% increased Energy Regeneration.
/// Seething Poison can only be active on 1 target at a time." Creeping Death adds 0.5% per 1% Haste.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 21 poisons): ticks every 2s shortened by Haste; 60s unhasted.
/// </summary>
public class SeethingPoison : MaraDot
{
    public const int EffectId = 1716;
    public const string EffectName = "Seething Poison";
    public const double Coefficient = 1.2276;
    public const double TickSeconds = 2.0;
    public const double PoisonSeconds = 60.0;
    public const double HemorrhageDamage = 1.2;
    public const int PredatorsRushEffectId = 1715;
    public const double PredatorsRushEnergyRegen = 1.2;

    /// <summary>Predator's Rush: on Mara while Seething Poison is on an enemy.</summary>
    public static readonly AuraDef PredatorsRush = AuraDef.Effect("Predator's Rush", PredatorsRushEffectId);

    public SeethingPoison(Mara owner) : base(owner, EffectId, EffectName, Coefficient, TickSeconds, PoisonSeconds)
    {
        HasCreepingDeath = true;
        owner.EnergyRegenMultipliers.Add(now => owner.Auras.HasAura(PredatorsRush, now) ? PredatorsRushEnergyRegen : 1.0);
        owner.Hemorrhage.TargetMultipliers.Add((enemy, ctx) => IsOn(enemy, ctx) ? HemorrhageDamage : 1.0);
        Removed += (ctx, _) => Ended(ctx);
    }

    /// <summary>Puts the poison on <paramref name="target"/>, taking it off the enemy that had it.</summary>
    public void ApplyTo(SimulationContext ctx, EnemyTarget target)
    {
        foreach (var other in Targets(ctx).Where(e => e != target).ToList()) Remove(ctx, other, expired: false);
        Apply(ctx, target);
        Mara.Auras.Apply(PredatorsRush, ctx.CurrentTime);
    }

    private void Ended(SimulationContext ctx)
    {
        if (ActiveCount(ctx) == 0) Mara.Auras.Remove(PredatorsRush, ctx.CurrentTime);
    }
}
