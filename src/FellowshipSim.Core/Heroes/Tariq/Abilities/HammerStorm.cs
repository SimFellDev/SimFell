namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1242: "Spin with your Hammer up to 3 times, dealing physical damage to all nearby enemies every 0.5 seconds.
/// Hammer Storm deals 35% more damage with each spin. Thunder Call: Hammer Storm causes lightning to strike you as you
/// spin, shocking enemies for an additional lightning damage each time they are hit." Costs 50 Fury.
/// No cooldown: ApplyCooldown 0x1450D9990 starts none for it (extractor 36f0a5a5, build 26320967).
/// Combat logs (1,600 intervals): as short as 0.668s.
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
    private int _channel;
    private bool _stopped;
    private SimTime _castTime;
    private double _spinPeriod;
    private double _castMultiplier = 1.0;
    private double _castCritChance;

    public HammerStorm(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power)
    {
        BaseCoefficient = Coefficient;
        SetChannel(ChannelSeconds, SpinPeriodSeconds);
        // Combat logs (3 Tariq fights, 229 channels): the first spin one server frame after the cast, 3 spins in all (see
        // OnChannelTick).
        ChannelTicksOnStart = true;
        // Combat logs (tests/ReactionTimeTests, 16 Tariq fights, 378 channels): the next cast comes a median 0.035s after
        // the channel ends, 207 of them within a server frame, as after a cast time; Rime's Freezing Torrent 0.171s.
        ChannelQueuesNextCast = true;
        AoeTargetThreshold = TargetThreshold;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason) =>
        CanPay(ctx, FocusedWrath.Cost(Tariq, FuryCost, ctx.CurrentTime), explain, out reason);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        // The spins come after the cast has resolved: they keep what the cast got (Focused Wrath, Schism).
        _castMultiplier = FocusedWrath.Spend(Tariq, FuryCost, ctx) * CastDamageMultiplier;
        _castCritChance = CurrentCast?.CritChance ?? 0.0;
        _spins = 0;
        _channel++;
        _stopped = false;
        _castTime = ctx.CurrentTime;
        _spinPeriod = SpinPeriodSeconds / (1.0 + Owner.GetHaste(ctx.CurrentTime));
    }

    /// <summary>
    /// The channel's first tick is the first spin; the others come every hasted half second from the cast, not from that
    /// tick (combat logs, fXyzwGcQkTrLD34R fight 4 @ 23.36: spins 0.033, 0.402 and 0.801s after the cast at 29% haste,
    /// each on the server frame after the cast + 0.388s per spin).
    /// </summary>
    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (_spins > 0) return;
        Spin(ctx, target);
        int channel = _channel;
        for (int k = 1; k < MaxSpins; k++)
        {
            ctx.ScheduleAt(_castTime + SimTime.FromSeconds(k * _spinPeriod), c =>
            {
                if (channel == _channel && !_stopped) Spin(c, target);
            }, Name);
        }
    }

    public override void OnChannelEnd(SimulationContext ctx, EnemyTarget target, bool clipped)
    {
        if (clipped) _stopped = true;
    }

    private void Spin(SimulationContext ctx, EnemyTarget target)
    {
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
