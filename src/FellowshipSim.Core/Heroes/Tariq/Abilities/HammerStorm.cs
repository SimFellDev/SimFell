namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1242: "Spin with your Hammer several times, dealing physical damage to all nearby enemies on a fixed period.
/// Hammer Storm deals more damage with each spin. Thunder Call: Hammer Storm causes lightning to strike you as you
/// spin, shocking enemies for an additional lightning damage each time they are hit." Costs Fury.
/// No cooldown: game data starts none for it.
/// </summary>
public class HammerStorm : TariqAbility
{
    public const int AbilityId = 1242;
    public const string AbilityName = "Hammer Storm";
    /// <summary>Share of Strength per spin.</summary>
    public const double Coefficient = 2.23;
    /// <summary>Share of Strength per spin.</summary>
    public const double LightningCoefficient = 0.78;
    public const double FuryCost = 50.0;
    /// <summary>Game data channelTime and channelTick (HamrStormPeriod, a tick rate).</summary>
    public const double ChannelSeconds = 1.0;
    public const double SpinPeriodSeconds = 0.5;
    public const int MaxSpins = 3;
    /// <summary>
    /// "More damage with each spin". Combat logs (fXyzwGcQkTrLD34R fight 4 @ 23.36, 10 enemies): each spin multiplies the
    /// last by this ramp.
    /// </summary>
    public const double SpinRamp = 1.35;
    /// <summary>Game data targetCountFalloff: effect 1782 has <see cref="TargetThreshold"/>; its lightning (effect 1781) has <see cref="LightningTargetThreshold"/>.</summary>
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
        // Combat logs (3 Tariq fights, 229 channels): the first spin one server frame after the cast, MaxSpins spins in all (see
        // OnChannelTick).
        ChannelTicksOnStart = true;
        // Combat logs (tests/ReactionTimeTests, 16 Tariq fights, 378 channels): the next cast comes shortly after
        // the channel ends, mostly within a server frame, as after a cast time; Rime's Freezing Torrent comes later.
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
    /// The channel's first tick is the first spin; the others come every hasted spin period from the cast, not from that
    /// tick (combat logs, fXyzwGcQkTrLD34R fight 4 @ 23.36: each spin on the server frame after the cast plus one hasted
    /// period per spin).
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
