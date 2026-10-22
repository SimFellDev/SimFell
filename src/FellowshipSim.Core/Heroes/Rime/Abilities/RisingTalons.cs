using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 2224 (Icy Talons): replaces Ice Comet. Ice spikes per Winter Orb on the target and all nearby enemies; costs
/// all Winter Orbs. It is an Ice Comet for talents (Avalanche, the spender crit buffs), so it builds on
/// <see cref="IceComet"/> and changes only the cost and the waves of spikes.
/// </summary>
public class RisingTalons : IceComet
{
    public new const int AbilityId = 2224;
    public new const string AbilityName = "Rising Talons";
    /// <summary>Per ice spike (40% of Ice Comet).</summary>
    public new const double Coefficient = 2.114;

    /// <summary>Combat logs: each spike loses damage as sqrt(12/N) beyond 12 targets.</summary>
    public new const double TargetThreshold = 12;

    /// <summary>
    /// Combat logs (10 Rime logs, 626 casts): one wave of spikes per Winter Orb; the first erupts 0.30-0.40s after the cast
    /// (server frames; 25th percentile 0.301s, median 0.334s), then 4-5 server frames apart regardless of haste (median
    /// 0.1447s). Each wave hits the enemies present when it erupts. The earliest usual times are used (0.30s, 4 frames),
    /// so a wave is never released after the moment it should land.
    /// </summary>
    public const double FirstWaveDelaySeconds = 0.30;
    public const double WaveIntervalSeconds = 4.0 / 30.0;

    public RisingTalons(Rime owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
    }

    protected override bool MeetsRequirements(bool explain, out string? reason)
    {
        if (!Rime.HasIcyTalons)
        {
            reason = explain ? $"{Name} needs Icy Talons" : null;
            return false;
        }
        if (Rime.WinterOrbs < 1)
        {
            reason = explain ? $"{Name} needs a Winter Orb" : null;
            return false;
        }
        reason = null;
        return true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        int spikes = Math.Max(1, Rime.WinterOrbs);
        Rime.SpendWinterOrbs(spikes, ctx);

        // Combat logs: every wave keeps the damage of the cast, even if buffs drop before it lands
        // (yMAZVJL9hTpFjqbC fight 7 @ 168.5, 207.4, 234.1), but hits the enemies present when it erupts
        // (@ 32.358: an enemy appearing after the cast is hit by every wave). The target falloff is fixed at the cast too
        // (PrkThcx8Y3RgVXyq @ 38.61: waves on 19, 7 and 3 enemies deal the same per hit).
        var now = ctx.CurrentTime;
        int enemiesAtCast = Math.Max(1, ctx.ActiveEnemies.Count);
        double falloff = enemiesAtCast > TargetThreshold ? Math.Sqrt(TargetThreshold / enemiesAtCast) : 1.0;
        double damage = BaseDamage(ctx, null) * CastDamageMultiplier * falloff;
        // The waves keep the crit chance of the cast, with what talents added to it (it counts as an Ice Comet for them).
        double critChance = Owner.CritChance(now) + CritChanceBonus + (CurrentCast?.CritChance ?? 0.0);
        double critMultiplier = CritMultiplierAt(now);
        for (int i = 0; i < spikes; i++)
        {
            bool first = i == 0;
            ctx.Schedule(SimTime.FromSeconds(FirstWaveDelaySeconds + i * WaveIntervalSeconds), c =>
            {
                // Each wave is an impact with the damage of the cast; the falloff is already in it.
                Impact(c, target, damage, critChance, critMultiplier, fixedFalloff: 1.0);
                if (first) RaiseStruck(c, target);
            }, Name);
        }
    }
}
