using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1027. Channel: ticks every 0.4s (hasted) for 2s, 1 Anima per tick.</summary>
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
    }

    /// <summary>Supreme Torrent: the channel lasts longer.</summary>
    public void ExtendChannel(double seconds) => ChannelDuration += SimTime.FromSeconds(seconds);

    private bool _soulfrost;
    private FlightOfTheNavir? _flightOfTheNavir;

    public override void Connect() => _flightOfTheNavir = Rime.GetAbility<FlightOfTheNavir>();

    /// <summary>
    /// The channel's final tick deals full damage instead of the part of a tick left (Supreme Torrent). Combat logs
    /// (PCaMVTFbZzNLrfx3 fight 5, 16 channels): with Supreme Torrent every final tick is as big as the ticks before it,
    /// even 0.03s after the previous one; without it (10 earlier logs) the final tick is partial.
    /// </summary>
    public bool FullFinalTick { get; set; }

    /// <summary>Soulfrost Torrent: the channel it empowers ticks 40% faster.</summary>
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
        // (yMAZVJL9hTpFjqbC fight 7 @ 49.522: 10 ticks, 10 hits; @ 34.229: one per tick until Flight of the Navir ends).
        _flightOfTheNavir?.CommandSwallow(ctx);
        Rime.AddAnima(AnimaPerTick, ctx, target);
        DealDamage(ctx, target, damageMultiplier: fraction, extraCritChance: _soulfrost ? SoulfrostTorrent.CritChance : 0.0);
    }
}
