using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1027. Effect 2315, its time rate on Bursting Ice, is unapplied: the ability holds it as its channel buff.
/// </summary>
public class FreezingTorrent : RimeAbility
{
    public const int AbilityId = 1027;
    public const string AbilityName = "Freezing Torrent";
    /// <summary>Per tick.</summary>
    public const double Coefficient = 1.4806;
    public const double CooldownSeconds = 15.0;
    public const double ChannelSeconds = 2.0;
    public const double TickPeriodSeconds = 0.4;
    public const double AnimaPerTick = 1.0;

    public FreezingTorrent(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        SetChannel(ChannelSeconds, TickPeriodSeconds);
        // Combat logs: a tick lands as the channel starts, then every hasted period, plus a partial final tick.
        ChannelTicksOnStart = true;
        ChannelPartialFinalTick = true;
        // The final tick comes when less than a frame of a period is left too, just after the regular tick at the end:
        // 67 Freezing Torrent channels of Rime's 15 fights end with two ticks on one enemy a few ms apart, at the end of the
        // channel with Supreme Torrent (both full: GWChJ8P7tn6fHgV3 #22 @ 116.838, 116.839) and without it
        // (the second small: NhBnFXmy3bJg9Cav #33 @ 127.877, 127.878).
        ChannelFinalTickAlways = true;
    }

    /// <summary>Supreme Torrent: the channel lasts longer.</summary>
    public void ExtendChannel(double seconds) => ChannelDuration += SimTime.FromSeconds(seconds);

    private bool _soulfrost;
    private FlightOfTheNavir? _flightOfTheNavir;

    public override void Connect() => _flightOfTheNavir = Rime.GetAbility<FlightOfTheNavir>();

    /// <summary>
    /// The channel's final tick deals full damage instead of the part of a tick left (Supreme Torrent). Combat logs
    /// (PCaMVTFbZzNLrfx3 fight 5, 16 channels): with Supreme Torrent every final tick is as big as the ticks before it,
    /// even when it follows the previous one closely; without it (10 earlier logs) the final tick is partial.
    /// </summary>
    public bool FullFinalTick { get; set; }

    /// <summary>Soulfrost Torrent: the channel it empowers ticks faster.</summary>
    public override double ChannelTickPeriodFor(SimulationContext ctx) =>
        _soulfrost ? ChannelTickPeriodSeconds / SoulfrostTorrent.TickRateMultiplier : ChannelTickPeriodSeconds;

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        _soulfrost = Rime.Auras.HasAura(SoulfrostTorrent.Buff, ctx.CurrentTime);
        if (_soulfrost) Rime.Auras.Remove(SoulfrostTorrent.Buff, ctx.CurrentTime);
    }

    public override void OnChannelEnd(SimulationContext ctx, EnemyTarget target, bool clipped) => _soulfrost = false;

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (FullFinalTick) fraction = 1.0;
        // Flight of the Navir: every tick (the partial one too) sends a swallow at an enemy of its choosing
        // (yMAZVJL9hTpFjqbC fight 7 @ 49.522: a hit per tick; @ 34.229: one per tick until Flight of the Navir ends).
        _flightOfTheNavir?.CommandSwallow(ctx);
        Rime.AddAnima(AnimaPerTick, ctx, target);
        if (!_soulfrost)
        {
            DealDamage(ctx, target, damageMultiplier: fraction);
            return;
        }
        DealDamage(ctx, target, damageMultiplier: fraction * SoulfrostTorrent.TickDamageScale(Owner, ctx.CurrentTime),
            extraCritChance: SoulfrostTorrent.CritChance);
    }
}
