using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1312: "Let loose a barrage of arrows in quick succession at your target, dealing physical damage" at a fixed
/// period while channeling. Costs Focus and has a cooldown. Its salvos can erupt (<see cref="LunarlightSalvo"/>).
/// A barrage cast with Impending Heartseeker (Impending Heartseeker, Starstriker's Ascent) uses it up, and each arrow deals
/// more than the one before.
/// </summary>
public partial class HeartseekerBarrage : ElarionAbility
{
    public const double Cost = 30.0;

    /// <summary>Crit chance added to the arrows (Fusillade).</summary>
    public double ArrowCritChance { get; set; }

    /// <summary>Arrows ricochet to one more enemy for this share of the arrow (Piercing Seekers; 0 = none).</summary>
    public double RicochetMultiplier { get; set; }

    /// <summary>
    /// One arrow's first hit dealt damage (Event Horizon). The ricochet carries the Bounce asset tag the manager excludes.
    /// </summary>
    public event Action<SimulationContext>? ArrowLanded;

    public HeartseekerBarrage(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = AgilityCoefficient;
        FocusCost = Cost;
        SetCooldown(AbilityCooldown);
        SetChannel(AbilityChannelTime, AbilityChannelTick);
        // Combat logs (pWmcV42K39GHTLQX fight 23 @ 81.335): a hasted channel fires its arrows on its target, all
        // of full damage: the part of a tick left at the end fires one more arrow.
        ChannelPartialFinalTick = true;
    }

    /// <summary>Fusillade: the channel lasts longer.</summary>
    public void ExtendChannel(double seconds) => ChannelDuration += SimTime.FromSeconds(seconds);

    private int _arrow;
    private bool _impending;
    private (double Damage, double Crit) _cast;

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        // Combat logs (pWmcV42K39GHTLQX fight 23 @ 15.192): Impending Heartseeker from this cast's own Spirit Refund
        // (Starstriker's Ascent) is left for the next barrage.
        _impending = Owner.Auras.HasAura(ImpendingHeartseeker.Buff, now);
        if (_impending) Owner.Auras.Remove(ImpendingHeartseeker.Buff, now);
        TriggerCooldown(ctx);
        Pay(ctx, CostAt(now));
        _arrow = 0;
        _cast = CastBonuses;
    }

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (target is not { IsSleeping: false }) return;
        _arrow++;
        // Impending Heartseeker: each arrow deals more than the one before. UNVERIFIED: the first arrow's increase.
        double multiplier = _cast.Damage * (_impending ? 1.0 + AdditionalDamagePerProjectile * _arrow : 1.0);
        double crit = _cast.Crit + ArrowCritChance;
        DealDamage(ctx, target, damageMultiplier: multiplier, extraCritChance: crit, onLanded: (c, dealt) =>
        {
            if (dealt > 0) ArrowLanded?.Invoke(c);
        });
        if (RicochetMultiplier <= 0) return;
        var others = ctx.ActiveEnemiesExcept(target);
        if (others.Count == 0) return;
        var next = ctx.Outcomes.ChooseTarget(DamageLogId, others, ctx);
        if (next == null || next == target) return;
        DealDamage(ctx, next, damageMultiplier: multiplier * RicochetMultiplier, extraCritChance: crit);
    }

    public override void ResetState()
    {
        _arrow = 0;
        _impending = false;
    }
}
