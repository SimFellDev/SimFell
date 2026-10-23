namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1242: "Spin with your Hammer up to 3 times, dealing physical damage to all nearby enemies every 0.5 seconds.
/// Hammer Storm deals 35% more damage with each spin. Thunder Call: Hammer Storm causes lightning to strike you as you
/// spin, shocking enemies for an additional lightning damage each time they are hit." Costs 50 Fury.
/// </summary>
public class HammerStorm : TariqAbility
{
    public const int AbilityId = 1242;
    public const string AbilityName = "Hammer Storm";
    /// <summary>Placeholder HamrStormTickDmg: 223% of Strength per spin.</summary>
    public const double Coefficient = 2.23;
    /// <summary>Placeholder HamrStormLightningDmg: 78% of Strength per spin.</summary>
    public const double LightningCoefficient = 0.78;
    public const double FuryCost = 50.0;
    /// <summary>Game data cooldown 1.5; haste shortens it, as it does Skull Crusher's.</summary>
    public const double CooldownSeconds = 1.5;
    /// <summary>Game data channelTime 1 and channelTick 0.5 (HamrStormPeriod, a tick rate).</summary>
    public const double ChannelSeconds = 1.0;
    public const double SpinPeriodSeconds = 0.5;
    public const int MaxSpins = 3;
    /// <summary>
    /// "35% more damage with each spin". Combat logs (fXyzwGcQkTrLD34R fight 4 @ 23.36, 10 enemies): spins of about
    /// 1.00, 1.37 and 1.88 times the first, so each multiplies the last by 1.35.
    /// </summary>
    public const double SpinRamp = 1.35;
    /// <summary>Game data targetCountFalloff: effect 1782 threshold 12; its lightning (effect 1781) threshold 15.</summary>
    public const double TargetThreshold = 12;
    public const double LightningTargetThreshold = 15;
    public const string LightningName = "Thunder Call: Hammer Storm";

    private int _spins;
    private double _castMultiplier = 1.0;
    private double _castCritChance;

    public HammerStorm(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        SetChannel(ChannelSeconds, SpinPeriodSeconds);
        // Combat logs (3 Tariq fights, 229 channels): the first spin one server frame after the cast, then every hasted
        // half second, 3 spins in all.
        ChannelTicksOnStart = true;
        AoeTargetThreshold = TargetThreshold;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason) =>
        CanPay(ctx, FocusedWrath.Cost(Tariq, FuryCost, ctx.CurrentTime), explain, out reason);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        // The spins come after the cast has resolved: they keep what the cast got (Focused Wrath, Schism).
        _castMultiplier = FocusedWrath.Spend(Tariq, FuryCost, ctx) * CastDamageMultiplier;
        _castCritChance = CurrentCast?.CritChance ?? 0.0;
        _spins = 0;
    }

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (_spins >= MaxSpins) return;
        double ramp = Math.Pow(SpinRamp, _spins++);
        double multiplier = _castMultiplier * ramp;
        var area = WildSwing.AreaAround(ctx, target);
        // Combat logs: every enemy's physical hit, then every enemy's lightning, all under the ability's ID.
        DealToEach(ctx, ctx.Outcomes.SelectTargets(DamageLogId, area, ctx), TargetThreshold, multiplier, extraCritChance: _castCritChance);
        var hero = Tariq;
        if (!hero.IsCharged(ctx.CurrentTime)) return;
        DealToEach(ctx, ctx.Outcomes.SelectTargets(DamageLogId, area, ctx), LightningTargetThreshold, multiplier * LightningCoefficient / Coefficient,
            overrideName: LightningName, extraCritChance: _castCritChance + hero.LightningCritChance);
    }
}
