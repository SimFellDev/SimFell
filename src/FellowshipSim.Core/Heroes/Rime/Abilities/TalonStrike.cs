using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 2223 (Icy Talons): replaces Glacial Blast. One ice spike per Winter Orb spent; costs all Winter Orbs. It is a
/// Glacial Blast for talents (Glacial Assault, the spender crit buffs), so it builds on <see cref="GlacialBlast"/> and
/// changes only the cost, the cast time and the spikes.
/// </summary>
public class TalonStrike : GlacialBlast
{
    public new const int AbilityId = 2223;
    public new const string AbilityName = "Talon Strike";
    /// <summary>Per ice spike (40% of Glacial Blast).</summary>
    public new const double Coefficient = 4.096;

    public TalonStrike(Rime owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
    }

    /// <summary>Instant.</summary>
    public override SimTime GetAdjustedCastTime(SimTime now = default) => SimTime.Zero;

    protected override bool MeetsRequirements(out string? reason)
    {
        if (!Rime.HasIcyTalons)
        {
            reason = $"{Name} needs Icy Talons";
            return false;
        }
        if (Rime.WinterOrbs < 1 && !GlacialAssaultReady)
        {
            reason = $"{Name} needs a Winter Orb";
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
        // With Glacial Assault no Winter Orbs change hands, so Wisdom of the North has nothing to count (game data
        // GA_Rime_Talent_SpendersSwap_SingleTarget: the proc path sets the cost to MaxResourcesTertiary only for the spike
        // count and writes no ResourcesTertiary; per the user, pending an in-game test).
        if (assault) multiplier = ConsumeGlacialAssault(ctx);
        else hero.SpendWinterOrbs(spikes, ctx);

        for (int i = 0; i < spikes; i++)
        {
            DealDamage(ctx, target, damageMultiplier: multiplier, onLanded: assault ? (c, dealt) => Explode(c, target, dealt) : null);
        }
    }
}
