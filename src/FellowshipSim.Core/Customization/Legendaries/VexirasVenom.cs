using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 420 (Mara's Wristbands of Vexira's Prey, item 5225; Hungering Blade Leggings, item 5900): "Your
/// Queen's Fang and Arachnid Assault critical strikes apply Vexira's Venom to enemies, adding a share of the initial damage
/// accumulatively as poison damage dealt over a duration" (effect 2364, ticking at an interval).
/// Combat logs (pWmcV42K39GHTLQX fight 23, 4,702 ticks): a tick deals a share of the venom, spaced by the interval shortened by Haste,
/// with a partial tick as it runs out (@ 15.962); a new crit restarts the duration
/// and keeps the tick timing. In the game data, the venom's ability
/// adds to and recalculates the periodic effect: with no running effect the tick is the added value
/// over the tick count, and on a running effect it is <see cref="RefreshedTick"/>. The call moves the start times
/// alone, leaves the period timer alone and applies nothing, so the log has no
/// refreshdebuff and `EffectLogic.CarryOverDuration` (stacking re-applications alone) gives no carry-over.
/// Combat logs: nearly all of 16,849 refreshes with one crit between two ticks, in the 19 Mara fights of `data/fights` with the
/// venom, match it (T2qLPpmMkrAtJ9B1 fight 18 @ 31.671). The ticks
/// never crit (effect 2364's preset `Empty`).
/// </summary>
public class VexirasVenom : Legendary
{
    public const int PowerId = 420;
    public const int WristItemId = 5225;
    public const int LegsItemId = 5900;
    public const int EffectId = 2364;
    public const string EffectName = "Vexira's Venom";
    /// <summary>Share of the initial damage, from game data.</summary>
    public const double DamageShare = 0.28;
    /// <summary>Venom duration, from game data.</summary>
    public const double VenomSeconds = 6.0;
    /// <summary>Tick interval from game data, divided by Haste.</summary>
    public const double TickSeconds = 2.0;
    /// <summary>The venom's damage is dealt in this many ticks' worth (the duration over the tick interval).</summary>
    public const double TicksPerVenom = VenomSeconds / TickSeconds;

    public override int LegendaryPowerId => PowerId;
    public override string Name => EffectName;
    public override string HeroName => "Mara";
    public override IReadOnlyList<int> ItemIds => [WristItemId, LegsItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Mara mara) return;
        var venom = new Venom(mara);
        mara.PoisonDamageIds.Add(venom.DamageLogId);
        mara.Register(venom.Debuff);
        foreach (var finisher in new Ability?[] { mara.GetAbility<QueensFang>(), mara.GetAbility<ArachnidAssault>() })
        {
            if (finisher == null) continue;
            // Mara's own crits, grievous ones too. The shadowy clones' add none: their hits have
            // CustomAssetTags.Mara.CopyCharacterDamage.Damage, and @ 20.935 a clone's
            // crit leaves the ticks as the Arachnid Assault crit before it made them. A hit with no ability under the
            // attack's name may be a clone's.
            string own = finisher.Name;
            finisher.Hit += (in hit) =>
            {
                if (hit.IsCrit && hit.Amount > 0 && hit.AbilityName == own && hit.AbilityGroup != null && hit.Target is { } target && hit.SimContext is { } ctx)
                {
                    venom.Add(ctx, target, DamageShare * hit.Amount);
                }
            };
        }
    }

    /// <summary>The venom on each enemy: a share of what it holds per tick.</summary>
    private sealed class Venom(Mara owner)
    {
        /// <summary>One application on one enemy, until its duration runs out.</summary>
        private sealed class Instance
        {
            public double TickValue;
            public SimTime EndsAt;
            public PeriodicEffect? Effect;
        }

        private readonly Dictionary<EnemyTarget, Instance> _active = new();
        private SimulationContext? _fight;

        public int DamageLogId { get; } = LogIds.Effect(EffectId);

        /// <summary>The debuff on the enemy, while the venom is on it.</summary>
        public AuraDef Debuff { get; } = AuraDef.Effect(EffectName, EffectId);

        public void Add(SimulationContext ctx, EnemyTarget target, double damage)
        {
            if (_fight != ctx)
            {
                _fight = ctx;
                _active.Clear();
                ctx.EnemyDemising += (c, enemy) =>
                {
                    if (_active.GetValueOrDefault(enemy) is { Effect: { } dying }) dying.Stop(c);
                };
            }
            var now = ctx.CurrentTime;
            if (_active.TryGetValue(target, out var dot) && dot.EndsAt > now && dot.Effect is { IsActive: true, PeriodTimer: { IsActive: true } timer } running)
            {
                dot.TickValue = RefreshedTick(dot.TickValue, damage, (dot.EndsAt - now).TotalSeconds, (timer.DueTime - now).TotalSeconds, (now - running.LastTickTime).TotalSeconds);
                dot.EndsAt = now + SimTime.FromSeconds(VenomSeconds);
                running.Refresh(ctx, VenomSeconds);
                target.Auras.Apply(Debuff, now);
                return;
            }
            var applied = new Instance { TickValue = damage / TicksPerVenom, EndsAt = now + SimTime.FromSeconds(VenomSeconds) };
            _active[target] = applied;
            target.Auras.Apply(Debuff, now);
            applied.Effect = PeriodicEffect.BeginHasted(ctx, owner, EffectName, VenomSeconds, TickSeconds, tickOnStart: false, partialFinalTick: true,
                (c, fraction) =>
                {
                    if (target.IsSleeping)
                    {
                        applied.Effect?.Stop(c);
                        return;
                    }
                    // The partial tick reads its progress on the frame it fires. Combat logs, 1,939 partial ticks
                    // in 19 Mara fights: fraction x period - time since the last tick has a mean near zero, and the
                    // 6,304 removals come a duration after the crit (NOTES.md).
                    if (fraction < 1.0) fraction = Math.Min(1.0, fraction + (c.CurrentTime - applied.EndsAt).TotalSeconds / applied.Effect!.PeriodSecondsAt(c.CurrentTime));
                    c.ApplyDamage(owner, target, applied.TickValue * fraction, DamageLogId, EffectName, canCrit: false, 0.0, 1.0);
                },
                (c, _) =>
                {
                    if (_active.GetValueOrDefault(target) != applied) return;
                    _active.Remove(target);
                    target.Auras.Remove(Debuff, c.CurrentTime);
                },
                damageLogId: DamageLogId, target: target, damageOverTime: true);
        }
    }

    /// <summary>
    /// The tick of a venom a crit refreshes: the ticks left of <paramref name="tick"/> and the crit's
    /// <paramref name="damage"/>, over the ticks of a new duration, with <paramref name="left"/> seconds left, the next
    /// tick <paramref name="nextTick"/> seconds away and the last <paramref name="sinceTick"/> seconds ago
    /// (the game: V_new = (V_old x W_old + added) / W_new).
    /// </summary>
    public static double RefreshedTick(double tick, double damage, double left, double nextTick, double sinceTick) =>
        (tick * TicksIn(left, nextTick, sinceTick) + damage) / TicksIn(VenomSeconds, nextTick, sinceTick);

    /// <summary>
    /// The ticks in <paramref name="seconds"/> of a running venom: the next tick with the unhasted ticks after it, or with no
    /// tick left to come, the time from the last tick in unhasted ticks (with the unhasted
    /// Period, the period timer's real time to the next tick, and its time since the last, a next tick under a period away).
    /// </summary>
    private static double TicksIn(double seconds, double nextTick, double sinceTick) =>
        seconds >= nextTick ? 1.0 + (seconds - nextTick) / TickSeconds : (seconds + sinceTick) / TickSeconds;
}
