namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Raging Tempest's Raging Currents (ability 1230, damage effect 1767): "Strike a random nearby enemy every 1 seconds
/// with lightning, dealing lightning damage with each strike. Each time Raging Currents hits an enemy your Expertise is
/// increased by 1%, stacking up to 20 times." Cast by <see cref="RagingTempest"/>, never on its own.
/// </summary>
public class RagingCurrents : TariqAbility
{
    public const int AbilityId = 1230;
    public const string AbilityName = "Raging Currents";
    /// <summary>Placeholder RagingCurDmg: 154% of Strength.</summary>
    public const double Coefficient = 1.54;
    public const int DamageEffectId = 1767;
    /// <summary>Placeholder constant PulsatingSingleDamage.PulsePeriod 1.</summary>
    public const double PeriodSeconds = 1.0;

    public RagingCurrents(Tariq owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        DamageLogId = LogIds.Effect(DamageEffectId);
    }

    /// <summary>One strike at a random enemy; a hit adds a stack of Raging Tempest.</summary>
    public void Strike(SimulationContext ctx)
    {
        var enemies = ctx.ActiveEnemies;
        var target = ctx.Outcomes.ChooseTarget(DamageLogId, enemies, ctx);
        if (target == null) return;
        DealDamage(ctx, target, extraCritChance: Tariq.LightningCritChance, onLanded: (c, dealt) =>
        {
            if (dealt > 0 && Owner.Auras.HasAura(RagingTempest.Buff, c.CurrentTime)) Owner.Auras.Apply(RagingTempest.Buff, c.CurrentTime);
        });
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => Strike(ctx);
}
