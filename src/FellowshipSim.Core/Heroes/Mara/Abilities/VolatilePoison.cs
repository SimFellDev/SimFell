namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1731, from Skittering Blades used in Stealth: the poison applies to all enemies hit by Skittering Blades, deals
/// damage over its duration, and erupts for damage on the target at expiration (Brooding Shadows' text); the eruption is
/// logged as effect 1730. Creeping Death adds damage per Haste.
/// In the game data, the duration of effect 1731 is rolled, and on
/// each application the tick period scaler is set to the duration over the tick count.
/// The scaler is stored as a tick rate override and re-arms the period:
/// new period = Period / Haste x scaler, and first delay = new period x remaining / old
/// rate, with no tick. A Haste change keeps the scaler. A re-application on a running poison rolls a new duration,
/// with no carry-over since the effect lacks `EffectLogic.CarryOverDuration`, and leaves the period timer
/// (the stack period reset policy is never reset), and the game data re-arms it with the new scaler. Each tick reads Mara's attributes at
/// the tick. The effect has `EffectType.Dot` and no
/// `EffectLogic.IgnorePartialTicks`, so it ends with a partial tick, factor elapsed / the period timer's rate.
/// The eruption happens on a natural expiry alone: it cancels on a premature removal, false at an expiry by duration,
/// true from `EffectLogic.RemoveOnDeath`'s removal, and a refresh removes nothing. It
/// delays the eruption by a random time up to <see cref="EruptionMaxDelaySeconds"/>, and applies effect 1730 to the poisoned enemy alone.
/// Combat logs, the 23 Mara fights of `data/fights`: the durations are spread evenly across the range from
/// <see cref="MinDurationSeconds"/> to <see cref="MaxDurationSeconds"/>, refreshes among them, and a poison ticks a fixed count scaled by Haste
/// whatever its duration. T2qLPpmMkrAtJ9B1 fight 18 @ 28.264: one cast gives
/// two enemies different durations with ticks at different spacings. Among the refreshes in 22 fights where
/// the rescaled time to the next tick and the old period's are clearly apart, the first tick is within a server
/// frame of the rescaled one more often than of the old period's (b649dhNptyW132Tm fight 5 @ 92.110). Eruptions come
/// a short delay after the removal.
/// </summary>
public class VolatilePoison : MaraAbility
{
    public const int EffectId = 1731;
    public const int EruptionEffectId = 1730;
    public const string EffectName = "Volatile Poison";
    /// <summary>
    /// Game data Agility coefficient. Combat logs (fight 19, 3,687 ticks): each tick deals the coefficient the
    /// text gives for the whole poison.
    /// </summary>
    public const double TickCoefficient = 0.312;
    /// <summary>Game data maximum ticks: the period is the duration / this, divided by Haste.</summary>
    public const int BaseTicks = 6;
    /// <summary>Game data eruption Agility coefficient.</summary>
    public const double EruptionCoefficient = 1.248;
    /// <summary>Game data minimum random duration.</summary>
    public const double MinDurationSeconds = 6.0;
    /// <summary>Game data maximum random duration.</summary>
    public const double MaxDurationSeconds = 8.0;
    /// <summary>Eruption delay, a random time from 0 to this.</summary>
    public const double EruptionMaxDelaySeconds = 0.2;

    public static readonly AuraDef Debuff = AuraDef.Effect(EffectName, EffectId);

    private readonly Eruption _eruption;
    private readonly Dictionary<EnemyTarget, PeriodicEffect> _active = new();
    private SimulationContext? _fight;

    public VolatilePoison(Mara owner) : base(owner, LogIds.Effect(EffectId), EffectName)
    {
        BaseCoefficient = TickCoefficient;
        HasCreepingDeath = true;
        _eruption = new Eruption(owner);
    }

    /// <summary>
    /// Puts the poison on <paramref name="target"/> for a rolled duration, the duration over <see cref="BaseTicks"/> the period. On an enemy that has it,
    /// the duration restarts with a new roll, and the time to the next tick keeps its fraction of the period.
    /// </summary>
    public void ApplyTo(SimulationContext ctx, EnemyTarget target)
    {
        if (_fight != ctx)
        {
            _active.Clear();
            _fight = ctx;
            ctx.EnemyDemising += (c, enemy) =>
            {
                if (_active.GetValueOrDefault(enemy) is { } dying) dying.Stop(c);
            };
        }
        double duration = ctx.Outcomes.RollDuration(new EffectDurationRequest(Owner, target, DamageLogId, MinDurationSeconds, MaxDurationSeconds), ctx);
        double period = duration / BaseTicks;
        target.Auras.Apply(Debuff, ctx.CurrentTime);
        if (_active.GetValueOrDefault(target) is { IsActive: true } running && running.Refresh(ctx, duration))
        {
            running.ChangePeriod(period);
            return;
        }
        PeriodicEffect? poison = null;
        poison = PeriodicEffect.BeginHasted(ctx, Owner, Name, duration, period, tickOnStart: false, partialFinalTick: true,
            (c, fraction) =>
            {
                if (target.IsSleeping)
                {
                    poison?.Stop(c);
                    return;
                }
                // The partial tick reads the period timer on the frame CheckDuration runs. Combat logs,
                // 7,615 partial ticks in 23 Mara fights: the engine's read as the duration runs out is low, and the read here matches.
                if (fraction < 1.0)
                {
                    double runOutAgo = (c.CurrentTime - poison!.End).TotalSeconds + AuraContainer.ExpiryLagSeconds;
                    fraction = Math.Min(1.0, fraction + runOutAgo / poison.PeriodSecondsAt(c.CurrentTime));
                }
                DealDamage(c, target, damageMultiplier: fraction);
            },
            (c, stopped) =>
            {
                // A poison applied after this one's duration ran out holds the enemy's place and its debuff.
                if (_active.GetValueOrDefault(target) == poison)
                {
                    _active.Remove(target);
                    target.Auras.Remove(Debuff, c.CurrentTime);
                }
                if (stopped) return;
                c.Schedule(SimTime.FromSeconds(c.Outcomes.RollDelay(EruptionMaxDelaySeconds, c)), cc =>
                {
                    if (!target.IsSleeping) _eruption.DealDamage(cc, target);
                }, "Volatile Poison erupts");
            },
            damageLogId: DamageLogId, target: target, damageOverTime: true);
        _active[target] = poison;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }

    /// <summary>The eruption: Agility damage on the poisoned enemy, logged as effect 1730.</summary>
    private sealed class Eruption : MaraAbility
    {
        public Eruption(Mara owner) : base(owner, LogIds.Effect(EruptionEffectId), EffectName)
        {
            BaseCoefficient = EruptionCoefficient;
            HasCreepingDeath = true;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
