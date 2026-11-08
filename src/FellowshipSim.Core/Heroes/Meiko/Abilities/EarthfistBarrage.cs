namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1112, a Fist Technique finisher (Earth Fist then Wind Kick): "Pummel your target with 4 quick punches, dealing
/// 126.96% of Agility physical damage every 0.2 seconds, leading in to a final powerful uppercut for 293.83% of Agility
/// damage. You can move while channeling Earthfist Barrage. Earthfist Barrage heals you for 50% of the damage it deals.
/// AFFECTED BY EARTHFALL: Earthfist Barrage deals +100% increased damage when it can consume a stack of Earthfall. Can be
/// used during Global Cooldown". A 1 second channel, cast without a cooldown: combat logs
/// (7Zmfx9gq6HjvThzc fight 2) have it cast 2.4-11 seconds apart (237.301, 239.672, 242.543, 250.895), against the game
/// data's 45 second cooldown.
/// </summary>
public partial class EarthfistBarrage : MeikoFinisher
{
    public const int Punches = 4;
    /// <summary>"+100% increased damage when it can consume a stack of Earthfall."</summary>
    public const double EarthfallMultiplier = 2.0;

    /// <summary>A punch or the uppercut struck the enemy (Earthwell, Forbidden Technique).</summary>
    public event Action<SimulationContext, EnemyTarget>? Punched;

    /// <summary>Multiplier on the uppercut alone (Tectonic Impact).</summary>
    public double UppercutMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Target falloff threshold of the uppercut's explosion onto the other enemies; 0 for no explosion (Tectonic Impact sets
    /// it).
    /// </summary>
    public double ExplosionTargetThreshold { get; set; }

    // The channel's damage multiplier (Earthfall and what talents added to the cast) and the hits it has dealt.
    private double _channelMultiplier = 1.0;
    private int _strikes;

    public EarthfistBarrage(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory, Technique.Fist, Technique.Kick)
    {
        BaseCoefficient = AgilityCoefficient;
        SetChannel(AbilityChannelTime, AbilityChannelTick);
        ChannelTicksOnStart = true;
    }

    /// <summary>
    /// The punches keep their <see cref="AbilityChannelTick"/> at every Haste: the channel's hasted period is undone here.
    /// Combat logs (27 fights) put the 5 hits 0.04, 0.27, 0.54, 0.77 and 1.01 seconds after every cast, at every Haste. All
    /// 5 are logged under 1112.
    /// </summary>
    public override double ChannelTickPeriodFor(SimulationContext ctx) => AbilityChannelTick * (1.0 + Owner.GetHaste(ctx.CurrentTime));

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        bool earthfall = Meiko.ConsumeCharge(RisingEarth.Earthfall, ctx, refundAfterSeconds: AbilityChannelTime);
        _channelMultiplier = (earthfall ? EarthfallMultiplier : 1.0) * CastDamageMultiplier;
        _strikes = 0;
        // Combat logs (27 fights): the next Combo Builder comes 1.0s or more after the cast, so the global cooldown lasts to
        // the channel's end.
        var channelEnd = ctx.CurrentTime + SimTime.FromSeconds(AbilityChannelTime);
        if (Owner.GlobalCooldownEndTime < channelEnd) Owner.GlobalCooldownEndTime = channelEnd;
    }

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (target is not { IsSleeping: false }) return;
        bool uppercut = _strikes++ >= Punches;
        double multiplier = _channelMultiplier * (uppercut ? FinalHitAgilityCoefficient / AgilityCoefficient * UppercutMultiplier : 1.0);
        DealDamage(ctx, target, multiplier);
        Punched?.Invoke(ctx, target);
        if (uppercut && ExplosionTargetThreshold > 0) Explode(ctx, target, multiplier);
    }

    /// <summary>The uppercut's damage explodes onto the enemies near <paramref name="target"/>, each hit with a crit roll of its own.</summary>
    private void Explode(SimulationContext ctx, EnemyTarget target, double multiplier)
    {
        var nearby = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
        int count = nearby.Count(e => e != target);
        if (count == 0) return;
        double falloff = count > ExplosionTargetThreshold ? Math.Sqrt(ExplosionTargetThreshold / count) : 1.0;
        foreach (var enemy in nearby)
        {
            if (enemy != target) DealDamage(ctx, enemy, multiplier * falloff);
        }
    }
}
