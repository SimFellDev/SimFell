namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1731, from Skittering Blades used in Stealth: "Applies Volatile Poison to all enemies hit by your Skittering
/// Blades, dealing 31.2% of Agility poison damage over 6 seconds. Upon expiration, Volatile Poison erupts dealing 124.8%
/// of Agility poison damage to the target" (Brooding Shadows' text); the eruption is logged as effect 1730. Creeping
/// Death adds 0.5% per 1% Haste.
/// In the game data (build 26320967), `GMMC_Mara_AoeAttackBuilder_Poison_DurationManager` rolls the duration of effect 1731, and on
/// each application `GA_Mara_Passive_Stealth_Poison_Manager` offsets 354 to 552 set the tick period scaler to the duration / 6.
/// `SetGameplayEffectCustomTickPeriodScaler` (0x145243620) stores it as `SetByCaller.System.TickRateOverride` and re-arms the period
/// (0x1452F2A70, 0x1452DC7E0): new period = Period / Haste x scaler, with Period 1, and first delay = new period x remaining / old
/// rate, with no tick. A Haste change keeps the scaler (0x1452DC8FC). A re-application on a running poison rolls a new duration,
/// with no carry-over since the effect lacks `EffectLogic.CarryOverDuration` (0x144A6A660), and leaves the period timer
/// (`StackPeriodResetPolicy` NeverReset), and the Blueprint re-arms it with the new scaler. Each tick reads Mara's attributes at
/// the tick (`RecaptureSourceAttributesBeforeEveryExecution`, 0x144A808C4). The effect has `EffectType.Dot` and no
/// `EffectLogic.IgnorePartialTicks`, so it ends with a partial tick, factor elapsed / the period timer's rate (0x1452D4B70).
/// `GA_Mara_Passive_AoeAttackBuilder_Poison_Aoe` erupts on a natural expiry alone: offset 37 cancels on `bPrematureRemoval`, false from
/// CheckDuration (0x144A6FAB0), true from `EffectLogic.RemoveOnDeath`'s removal (0x144A80C20), and a refresh removes nothing. Offset 75
/// delays it 0 to 0.2s, and offset 1122 applies effect 1730 to the poisoned enemy alone.
/// Combat logs, the 23 Mara fights of `data/fights`: the 10,664 durations are spread evenly from 6.000s to 7.999s, 1,257 refreshes
/// among them, and a poison ticks 6 x (1 + Haste) times whatever its duration. T2qLPpmMkrAtJ9B1 fight 18 @ 28.264: one cast gives
/// enemy 8.1 a duration of 6.089s with ticks 0.87s apart and enemy 8.2 7.980s with ticks 1.14s apart. Over the 181 refreshes where
/// the rescaled time to the next tick and the old period's are 0.1s or more apart, in 22 fights, the first tick is within a server
/// frame of the rescaled one 73 times and of the old period's 14 times (b649dhNptyW132Tm fight 5 @ 92.110). 8,218 eruptions come
/// 0.008s to 0.228s after the removal.
/// </summary>
public class VolatilePoison : MaraAbility
{
    public const int EffectId = 1731;
    public const int EruptionEffectId = 1730;
    public const string EffectName = "Volatile Poison";
    /// <summary>
    /// AC_Hero_Mara Mara.AoeAttackBuilder.Poison.AgilityCoefficient. Combat logs (fight 19, 3,687 ticks): each tick deals the 31.2% the
    /// text gives for the whole poison.
    /// </summary>
    public const double TickCoefficient = 0.312;
    /// <summary>AC_Hero_Mara Mara.AoeAttackBuilder.Poison.MaxTicks: the period is the duration / this, divided by Haste.</summary>
    public const int BaseTicks = 6;
    /// <summary>AC_Hero_Mara Mara.AoeAttackBuilder.Poison.Aoe.AgilityCoefficient.</summary>
    public const double EruptionCoefficient = 1.248;
    /// <summary>AC_Hero_Mara Mara.AoeAttackBuilder.Poison.MinRandomDuration.</summary>
    public const double MinDurationSeconds = 6.0;
    /// <summary>AC_Hero_Mara Mara.AoeAttackBuilder.Poison.MaxRandomDuration.</summary>
    public const double MaxDurationSeconds = 8.0;
    /// <summary>`GA_Mara_Passive_AoeAttackBuilder_Poison_Aoe` offset 75, `RandomFloatInRange(0, 0.2)`.</summary>
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
    /// Puts the poison on <paramref name="target"/> for a rolled duration, a sixth of it the period. On an enemy that has it,
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
        if (_active.GetValueOrDefault(target) is { IsActive: true } running)
        {
            running.ChangePeriod(period);
            running.RestartDuration(ctx, duration);
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
                // The partial tick reads the period timer on the frame CheckDuration runs (0x144A6FAB0, 0x1452D4B70). Combat logs,
                // 7,615 partial ticks in 23 Mara fights: 1.075x the engine's read as the duration runs out, 1.028x read here.
                if (fraction < 1.0)
                {
                    double runOutAgo = (c.CurrentTime - poison!.End).TotalSeconds + AuraContainer.ExpiryLagSeconds;
                    fraction = Math.Min(1.0, fraction + runOutAgo / poison.PeriodSecondsAt(c.CurrentTime));
                }
                DealDamage(c, target, damageMultiplier: fraction);
            },
            (c, stopped) =>
            {
                _active.Remove(target);
                target.Auras.Remove(Debuff, c.CurrentTime);
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

    /// <summary>The eruption: 124.8% of Agility on the poisoned enemy, logged as effect 1730.</summary>
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
