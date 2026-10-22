namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1018. Costs 2 Winter Orbs. A comet falls on the target and strikes every enemy around it. Also the free comets
/// of Cold Shower and Avalanche, which stay plain Ice Comets even with Icy Talons (per the user); see
/// <see cref="FreeCast"/>. Rising Talons (Icy Talons) is built on it.
/// </summary>
public class IceComet : RimeAbility
{
    public const int AbilityId = 1018;
    public const string AbilityName = "Ice Comet";
    public const double Coefficient = 5.285;
    public const int DefaultWinterOrbCost = 2;

    /// <summary>hero_data Ice Comet TargetCountThreshold 12: damage falls off as sqrt(12/N) beyond 12 targets.</summary>
    public const double TargetThreshold = 12;

    /// <summary>
    /// Ability 2227 ("Ice Comet", GA_Rime_Util_OnTargetPulsatingAoe): combat logs report every comet's damage under it,
    /// cast or free (casts are logged as 1018).
    /// </summary>
    public const int DamageId = 2227;

    /// <summary>
    /// The comet falls before it strikes: 0.30s plus 1.0s divided by (1 + haste) at the cast, with damage and crit chance
    /// snapshot at the cast. Combat logs (n7zxK8harZT2wjDB fight 9, PCaMVTFbZzNLrfx3 fight 5; 193 casts): impact within a
    /// few server frames of this; Icy Flow's "0.5 seconds reduced impact delay" comes off the hasted part (0.97s to
    /// 0.47s). The fixed part is the earliest usual time, so an impact is never released after it should land.
    /// </summary>
    public const double ImpactDelayFixedSeconds = 0.30;
    public const double ImpactDelayHastedSeconds = 1.0;

    /// <summary>
    /// An Ice Comet struck its target: a cast comet or Cold Shower's on impact, Rising Talons' first wave (Avalanche).
    /// </summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public IceComet(Rime owner) : this(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        DamageLogId = DamageId;
    }

    /// <summary>For abilities built on Ice Comet (Rising Talons), which set their own values.</summary>
    protected IceComet(Rime owner, int id, string name) : base(owner, id, name, AbilityCategory.Power) { }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason) =>
        MeetsRequirements(explain, out reason) && base.CanCast(ctx, explain, out reason);

    /// <summary>Talent and Winter Orb requirements, checked before the cooldown.</summary>
    protected virtual bool MeetsRequirements(bool explain, out string? reason)
    {
        if (Rime.HasIcyTalons)
        {
            reason = explain ? $"{Name} is replaced by Rising Talons (Icy Talons)" : null;
            return false;
        }
        if (Rime.WinterOrbs < DefaultWinterOrbCost)
        {
            reason = explain ? $"{Name} needs {DefaultWinterOrbCost} Winter Orbs (have {Rime.WinterOrbs})" : null;
            return false;
        }
        reason = null;
        return true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.SpendWinterOrbs(DefaultWinterOrbCost, ctx);
        Launch(ctx, target, ImpactDelay(ctx), raisesStruck: true);
    }

    /// <summary>
    /// A free comet from a talent's proc (<paramref name="source"/>): no cost, and it resolves as an Ice Comet cast, so
    /// talents that add to the next Ice Comet apply. It falls like a cast comet unless <paramref name="impactDelaySeconds"/>
    /// says when it strikes. With <paramref name="countsAsCast"/> its impact raises <see cref="Struck"/> (Cold Shower's
    /// comet can trigger Avalanche; Avalanche's own comets cannot).
    /// </summary>
    public void FreeCast(SimulationContext ctx, EnemyTarget target, object source, bool countsAsCast, double? impactDelaySeconds = null) =>
        ResolveCast(ctx, target, source, () => Launch(ctx, target, impactDelaySeconds ?? ImpactDelay(ctx), countsAsCast));

    /// <summary>Seconds from the cast to the impact (the hasted part shortened by what talents took off the cast).</summary>
    private double ImpactDelay(SimulationContext ctx)
    {
        double hasted = Math.Max(0.0, ImpactDelayHastedSeconds - (CurrentCast?.DelayReductionSeconds ?? 0.0));
        return ImpactDelayFixedSeconds + hasted / (1.0 + Owner.GetHaste(ctx.CurrentTime));
    }

    /// <summary>Snapshots the comet's damage and crit now; it strikes after <paramref name="delaySeconds"/>.</summary>
    private void Launch(SimulationContext ctx, EnemyTarget target, double delaySeconds, bool raisesStruck)
    {
        var now = ctx.CurrentTime;
        double damage = BaseDamage(ctx, null) * CastDamageMultiplier;
        double critChance = Owner.CritChance(now) + CritChanceBonus + (CurrentCast?.CritChance ?? 0.0);
        double critMultiplier = CritMultiplierAt(now);
        ctx.Schedule(SimTime.FromSeconds(delaySeconds), c =>
        {
            Impact(c, target, damage, critChance, critMultiplier, fixedFalloff: null);
            if (raisesStruck) RaiseStruck(c, target);
        }, $"{Name} impact");
    }

    protected void RaiseStruck(SimulationContext ctx, EnemyTarget target)
    {
        if (target != null) Struck?.Invoke(ctx, target);
    }

    /// <summary>
    /// One impact with damage, crit chance and crit power snapshot earlier: strikes the enemies present now. The target
    /// falloff is sqrt(T/N) over the enemies struck, or <paramref name="fixedFalloff"/> when it was fixed earlier.
    /// </summary>
    protected void Impact(SimulationContext ctx, EnemyTarget target, double damage, double critChance, double critMultiplier, double? fixedFalloff)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        double falloff = fixedFalloff ?? (AoeTargetThreshold > 0 && struck.Count > AoeTargetThreshold ? Math.Sqrt(AoeTargetThreshold / struck.Count) : 1.0);
        foreach (var enemy in struck)
        {
            ctx.ApplyDamage(Owner, enemy, DamageLogId, Name, canCrit: true,
                c => new SimulationContext.HitNumbers(
                    damage * falloff * Owner.DamageMultiplierAgainst(enemy, c.CurrentTime),
                    critChance + Owner.CritChanceAgainst(enemy, c.CurrentTime) - Owner.CritChance(c.CurrentTime),
                    critMultiplier),
                snapshot: true);
        }
    }
}
