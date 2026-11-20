using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 2223 (Icy Talons): replaces Glacial Blast. One ice spike per Winter Orb spent; costs all Winter Orbs. It is a
/// Glacial Blast for talents (Glacial Assault, the spender crit buffs), so it builds on <see cref="GlacialBlast"/> and
/// changes only the cost, the cast time and the spikes.
/// </summary>
public partial class TalonStrike : GlacialBlast
{
    /// <summary>Per ice spike (a fraction of Glacial Blast's).</summary>
    public const double Coefficient = 4.096;

    public TalonStrike(Rime owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
    }

    /// <summary>Instant.</summary>
    public override SimTime GetAdjustedCastTime(SimTime now = default) => SimTime.Zero;

    protected override bool MeetsRequirements(bool explain, out string? reason)
    {
        if (!Rime.HasIcyTalons)
        {
            reason = explain ? $"{Name} needs Icy Talons" : null;
            return false;
        }
        if (Rime.WinterOrbs < 1 && !GlacialAssaultReady)
        {
            reason = explain ? $"{Name} needs a Winter Orb" : null;
            return false;
        }
        reason = null;
        return true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Rime;
        bool assault = GlacialAssaultReady;
        // With Glacial Assault it is free and fires as if cast with full Winter Orbs (per the user; Rising Talons never
        // consumes Glacial Assault in the logs).
        int spikes = assault ? Rime.MaxWinterOrbs : Math.Max(1, hero.WinterOrbs);
        double multiplier = 1.0;
        if (assault)
        {
            multiplier = ConsumeGlacialAssault(ctx);
            // INFERRED from logs (not the tooltip): cast "as if" with full Winter Orbs, it counts them as spent for Wisdom of
            // the North. Across 10 logs this removes most Ice Blitz cooldown misses while some casts still land right
            // as Ice Blitz comes back (so it is not too fast).
            hero.NotifyResourceSpent(Rime.WinterOrbsResource, spikes, ctx.CurrentTime);
        }
        else
        {
            hero.SpendWinterOrbs(spikes, ctx);
        }

        for (int i = 0; i < spikes; i++)
        {
            DealDamage(ctx, target, damageMultiplier: multiplier, onLanded: assault ? (c, dealt) => Explode(c, target, dealt) : null);
        }
    }
}
