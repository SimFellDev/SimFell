namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Effect 1276, Sweeping Strike's bleed: "Enemies hit by Sweeping Strike bleed for 60% of Strength physical damage every 1.5
/// seconds for 12 seconds" (EffectType.Dot, EffectLogic.CarryOverDuration). One bleed on each enemy, ticking every hasted
/// period, with a partial final tick. Sweeping Strike applies it.
/// </summary>
public class SweepingStrikeBleed : HelenaAbility
{
    public const int EffectId = 1276;
    public const string EffectName = "Sweeping Strike Bleed";
    public const double Coefficient = 0.6;
    public const double PeriodSeconds = 1.5;
    public const double DurationSeconds = 12.0;

    /// <summary>The bleed on the enemy. Action lists read it.</summary>
    public static readonly AuraDef Debuff = AuraDef.Effect("Sweeping Strike", EffectId, DurationSeconds);

    private readonly Dictionary<EnemyTarget, PeriodicEffect> _active = new();
    private SimulationContext? _fight;

    public SweepingStrikeBleed(Helena owner) : base(owner, LogIds.Effect(EffectId), EffectName)
    {
        BaseCoefficient = Coefficient;
        DamageLogId = LogIds.Effect(EffectId);
        owner.DotDamageIds.Add(DamageLogId);
    }

    private void SameFight(SimulationContext ctx)
    {
        if (_fight == ctx) return;
        _active.Clear();
        ctx.EnemyDemising += (c, enemy) =>
        {
            if (_active.Remove(enemy, out var bleed)) bleed.Stop(c);
        };
        _fight = ctx;
    }

    /// <summary>Whether the bleed is on <paramref name="target"/> (Sharpened Blade).</summary>
    public bool IsOn(EnemyTarget? target, SimulationContext ctx)
    {
        SameFight(ctx);
        return target != null && _active.TryGetValue(target, out var bleed) && bleed.IsActive;
    }

    /// <summary>
    /// Bleeds <paramref name="target"/> for the full duration. On an enemy already bleeding, the time left carries over
    /// (EffectLogic.CarryOverDuration). UNVERIFIED: no log has been read for how much carries over.
    /// </summary>
    public void ApplyTo(SimulationContext ctx, EnemyTarget target)
    {
        SameFight(ctx);
        var now = ctx.CurrentTime;
        double duration = DurationSeconds;
        if (_active.Remove(target, out var old))
        {
            if (old.IsActive) duration += Math.Max(0.0, (old.End - now).TotalSeconds);
            old.Stop(ctx);
        }
        target.Auras.Apply(Debuff.WithDuration(duration), now);
        PeriodicEffect? bleed = null;
        bleed = PeriodicEffect.BeginHasted(ctx, Owner, Name, duration, PeriodSeconds, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) =>
            {
                if (!target.IsSleeping) DealDamage(c, target, damageMultiplier: fraction);
            },
            onEnd: (c, _) =>
            {
                if (_active.GetValueOrDefault(target) == bleed) _active.Remove(target);
            },
            damageLogId: DamageLogId, target: target, damageOverTime: true);
        _active[target] = bleed;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }

    /// <summary>Sweeping Strike alone applies it.</summary>
    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        reason = explain ? $"{Name} is applied by Sweeping Strike" : null;
        return false;
    }
}
