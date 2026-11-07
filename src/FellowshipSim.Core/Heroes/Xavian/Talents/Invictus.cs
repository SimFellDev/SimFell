using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 604: "Your Solar Shield gains additional benefits from your secondary stats. Critical Strike Chance: When you
/// activate Solar Shield you deal damage to all nearby enemies equal to 1% of the maximum absorb value for every 1%
/// Critical Strike chance you have. Haste: While Solar Shield is active on anyone, your Cooldown Recovery is increased by
/// 1% for every 3% Haste you have. Expertise: While Solar Shield is active, the target's Armor is increased by an amount
/// equal to your Expertise %. Spirit: Your Solar Shield ability cooldown is reduced by 2 seconds each time you trigger a
/// Spirit Refund." The damage is effect 2721 (targetCountFalloff threshold 1); effect 2551 Haste.HasteToCooldownRecoveryScaler
/// 0.33334.
/// </summary>
public class Invictus : Talent
{
    public const int TalentId = 604;
    public const string TalentName = "Invictus";
    public const int DamageEffectId = 2721;
    /// <summary>1% of the maximum absorb for every 1% Critical Strike chance.</summary>
    public const double AbsorbPerCrit = 1.0;
    /// <summary>Game data targetCountFalloff: effect 2721, threshold 1.</summary>
    public const double TargetThreshold = 1;
    /// <summary>Effect 2551 talent constant Haste.HasteToCooldownRecoveryScaler.</summary>
    public const double CooldownRecoveryPerHaste = 0.33334;
    public const double SpiritRefundCooldownSeconds = 2.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        var burst = new SolarBurst(xavian);
        xavian.SolarShield.Shielded += burst.Strike;
        hero.CooldownRecoveryBonuses.Add(now =>
            hero.Auras.HasAura(SolarShield.Buff, now) ? CooldownRecoveryPerHaste * hero.GetHaste(now) : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(SolarShield.Buff.Key);
        hero.OnSpiritRefund += (_, _, ctx) =>
            hero.ReduceCooldown<SolarShield>(SimTime.FromSeconds(SpiritRefundCooldownSeconds), ctx.CurrentTime);
    }

    /// <summary>
    /// The damage as a shield goes up, on every enemy, losing damage as sqrt(1/N) over N enemies. It has the calculation
    /// preset FlatDamageHealingScaling (GE_Sune_Talent_SingleTargetAbsorb_AoeDamage_Damage; DT_EffectCalculationPresets:
    /// CanCrit false, DefaultSpread 0, ApplySourceDamageHealingMultipliers and ApplySourceExpertiseMultiplier true), so
    /// Expertise increases it once more after the absorb. Combat logs (16 Xavian players, 2,000+ casts): every enemy takes the
    /// same amount, never a crit, sqrt(1/N) of it; log/engine 0.92-1.09 a fight.
    /// </summary>
    private sealed class SolarBurst : Ability
    {
        private double _absorb;

        public SolarBurst(Xavian owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            ScalesWithExpertise = false;
        }

        public void Strike(SimulationContext ctx, double absorb)
        {
            if (ctx.ActiveEnemies.Count == 0) return;
            _absorb = absorb;
            var struck = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemies, ctx);
            double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
            for (int i = 0; i < struck.Count; i++) DealDamage(ctx, struck[i], falloff, forcedCrit: false);
        }

        public override double BaseDamage(SimulationContext ctx, EnemyTarget? target, double damageMultiplier = 1.0)
        {
            var now = ctx.CurrentTime;
            return _absorb * AbsorbPerCrit * Owner.CritChance(now)
                   * (1.0 + Owner.Expertise(now))
                   * DamageScaleFor(ctx)
                   * Owner.DamageMultiplierAgainst(target, now)
                   * Owner.AbilityDamageMultiplier(this, now)
                   * DamageMultiplier
                   * damageMultiplier;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
