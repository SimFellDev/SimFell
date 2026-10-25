namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 28 (ability 172 GA_Trait_ExtraDotHotOnEffectApplicationProc, damage effect 104): "Your Damage Effects have a
/// chance (2.1 PPM) to burn the target, dealing 483% / 882% / 1281% / 1680% of your Main Stat in fire damage to them over
/// 9 seconds" (the heal over time does not affect damage). Each application of a harmful effect rolls, a damage over time
/// tick does not (the applied and refresh listeners at ability 172 offsets 859 and 1669, InTriggerPeriodicExecutions
/// false), on one hasted proc timer for every target (RollProcPerMinute, ScaleWithHaste true).
/// </summary>
public class Kindling : Sigil
{
    public const int SigilId = 28;
    public const string SigilName = "Kindling";
    public const int DamageEffectId = 104;
    /// <summary>Effect 104 DurationMagnitude, row Trait.ExtraDotHotOnEffectApplicationProc.Dot.Duration; not hasted.</summary>
    public const double DurationSeconds = 9.0;
    /// <summary>
    /// Effect 104 Period, row Trait.ExtraDotHotOnEffectApplicationProc.Hot.Period; hasted, no tick on application, a
    /// partial final tick (asset tag EffectType.Dot).
    /// </summary>
    public const double TickSeconds = 1.5;
    /// <summary>Row Trait.ExtraDotHotOnEffectApplicationProc.PPM (TraitConstants), 2.1 at every rank.</summary>
    public const double ProcsPerMinute = 2.1;
    public const string ProcKey = "kindling";
    /// <summary>
    /// Per tick, x Main Stat: effect 104's DamageScale modifier, row Trait.ExtraDotHotOnEffectApplicationProc.Dot.FlatDamageValue
    /// (the tooltip's 483% / 882% / 1281% / 1680% are 7 of them), preset HeroMagicalOT (crits, Expertise).
    /// </summary>
    public static readonly double[] CoefficientByRank = [0.69, 1.26, 1.83, 2.40];

    public override int Id => SigilId;
    public override string Name => SigilName;

    /// <summary>
    /// The burn on one enemy. A new proc on a burning enemy restarts the 9 seconds and keeps the tick timer (effect 104
    /// StackPeriodResetPolicy NeverReset, StackDurationRefreshPolicy RefreshOnSuccessfulApplication; combat logs,
    /// GWChJ8P7tn6fHgV3 fight 20 @ 28.823: the ticks keep their rhythm and the burn ends 9.019s after the refresh).
    /// </summary>
    private sealed class Burn
    {
        public SimTime EndsAt;
        public HastedTimer? NextTick;
        public ScheduledEvent? End;
        public bool Active;
    }

    public override void Bind(Hero hero)
    {
        var dot = new KindlingDot(hero, ForRank(CoefficientByRank));
        hero.DotDamageIds.Add(dot.DamageLogId);
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        var burning = new Dictionary<EnemyTarget, Burn>();
        // The fight the burning enemies belong to (each sim iteration and replay has its own).
        SimulationContext? fight = null;

        void Tick(SimulationContext c, EnemyTarget target, Burn burn)
        {
            if (!burn.Active || c.CurrentTime > burn.EndsAt)
            {
                burn.NextTick?.Cancel();
                burn.NextTick = null;
                return;
            }
            if (!target.IsSleeping) dot.DealDamage(c, target);
        }

        // The duration runs out at its end; the partial tick (the hasted progress left) comes on the next server frame.
        void RunOut(SimulationContext c, EnemyTarget target, Burn burn)
        {
            if (!burn.Active) return;
            burn.Active = false;
            double fraction = burn.NextTick is { IsActive: true } timer ? timer.ElapsedBaseSeconds / TickSeconds : 0.0;
            // The partial tick reads how long the period has run (period - remaining, 0x1452D0C70) as the duration runs out.
            double elapsed = fraction * TickSeconds / (1.0 + hero.GetHaste(c.CurrentTime));
            burn.NextTick?.Cancel();
            burn.NextTick = null;
            if (fraction <= 0 || elapsed < PeriodicEffect.MinHastedPartialTickSeconds - 1e-9) return;
            fraction = Math.Min(fraction, 1.0);
            c.Schedule(SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds), cc =>
            {
                if (target.IsSleeping) return;
                cc.CurrentTickFraction = fraction;
                try { dot.DealDamage(cc, target, damageMultiplier: fraction); }
                finally { cc.CurrentTickFraction = 1.0; }
            }, SigilName);
        }

        void Ignite(SimulationContext ctx, EnemyTarget target)
        {
            var now = ctx.CurrentTime;
            if (!burning.TryGetValue(target, out var burn) || !burn.Active)
            {
                burn = new Burn { Active = true };
                burning[target] = burn;
                var started = burn;
                // Effect 104 is tagged EffectType.Dot: its ticks run on the game's period timer.
                burn.NextTick = HastedTimer.StartPeriod(ctx, hero, TickSeconds, c => Tick(c, target, started), SigilName, dot.DamageLogId);
            }
            burn.End?.Cancel();
            burn.EndsAt = now + SimTime.FromSeconds(DurationSeconds);
            var current = burn;
            burn.End = ctx.ScheduleAt(burn.EndsAt, c => RunOut(c, target, current), SigilName);
        }

        void Roll(SimulationContext ctx, EnemyTarget target)
        {
            if (fight != ctx)
            {
                fight = ctx;
                burning.Clear();
            }
            if (rppm.Roll(hero, ctx, target)) Ignite(ctx, target);
        }

        // Speed: the handler captures nothing per hit; the burn's closures are made only when it ignites.
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.Target is not { } target || dmg.SimContext is not { } ctx || hero.DotDamageIds.Contains(dmg.AbilityId)) return;
            Roll(ctx, target);
        };
        hero.OnHarmfulEffectApplied += Roll;
    }

    private sealed class KindlingDot : Ability
    {
        public KindlingDot(Hero owner, double coefficient) : base(owner, LogIds.Effect(DamageEffectId), SigilName)
        {
            BaseCoefficient = coefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
