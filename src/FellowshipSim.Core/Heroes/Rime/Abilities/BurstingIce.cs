using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1031. Applies debuff effect 1395 (3s, pulsing every 0.5s hasted) whose pulses deal damage effect 1396
/// to the target and nearby enemies and generate 1 Anima. While it is active Rime has Winter's Embrace (effect 2303).
/// </summary>
public class BurstingIce : RimeAbility
{
    public const int AbilityId = 1031;
    public const string AbilityName = "Bursting Ice";
    public const double Coefficient = 0.498;
    public const double CooldownSeconds = 10.0;
    public const double CastTimeSeconds = 2.0;
    public const int DebuffEffectId = 1395;
    public const double DurationSeconds = 3.0;
    public const double PulsePeriodSeconds = 0.5;
    public const int PulseEffectId = 1396;
    public const double AnimaPerPulse = 1.0;
    public const int WintersEmbraceEffectId = 2303;
    public const double WintersEmbraceDamage = 1.2;
    /// <summary>Combat logs: pulses lose damage as sqrt(5/N) beyond 5 targets.</summary>
    public const double TargetThreshold = 5;

    /// <summary>The debuff on the enemy. Action lists read it; the pulses run on their own timer.</summary>
    public static readonly AuraDef Debuff = AuraDef.Effect("Bursting Ice", DebuffEffectId, DurationSeconds);

    /// <summary>"While Bursting Ice is active on an enemy you gain Winter's Embrace, causing you to deal 20% more damage."</summary>
    public static readonly AuraDef WintersEmbrace = AuraDef.Effect("Winter's Embrace", WintersEmbraceEffectId,
        modifiers: StatModifier.More(StatType.DamageScale, WintersEmbraceDamage));

    private int _activeDebuffs;
    private double _bonusDurationSeconds;
    private AuraDef _debuff = Debuff;

    /// <summary>Seconds added to the debuff and its pulses (Skandi's Decree).</summary>
    public double BonusDurationSeconds
    {
        get => _bonusDurationSeconds;
        set
        {
            _bonusDurationSeconds = value;
            _debuff = value == 0 ? Debuff : Debuff with { Duration = SimTime.FromSeconds(DurationSeconds + value) };
        }
    }

    /// <summary>Multipliers on each pulse's damage, read as the pulse goes off (Harrowing Ice).</summary>
    public List<Func<SimulationContext, double>> PulseDamageMultipliers { get; } = new();

    /// <summary>Raised after each pulse has dealt its damage (Harrowing Ice).</summary>
    public event Action<SimulationContext>? Pulsed;

    public BurstingIce(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds);
        AoeTargetThreshold = TargetThreshold;
        DamageLogId = LogIds.Effect(PulseEffectId);
    }

    /// <summary>One pulse around <paramref name="target"/>; <paramref name="fraction"/> scales a partial final pulse.</summary>
    public void DealPulse(SimulationContext ctx, EnemyTarget? target, double fraction = 1.0)
    {
        var hero = Rime;
        hero.AddAnima(AnimaPerPulse, ctx, target);
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target != null ? [target] : ctx.ActiveEnemies;

        double multiplier = fraction;
        foreach (var pulseMultiplier in PulseDamageMultipliers) multiplier *= pulseMultiplier(ctx);
        DealAoeDamage(ctx, enemies, damageMultiplier: multiplier);
        Pulsed?.Invoke(ctx);
    }

    /// <summary>"Winter's Embrace does not affect Bursting Ice."</summary>
    protected override double DamageScaleFor(SimulationContext ctx)
    {
        double scale = Owner.DamageScale(ctx.CurrentTime);
        if (Rime.Auras.HasAura(WintersEmbrace, ctx.CurrentTime))
        {
            var mods = WintersEmbrace.Modifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].Type == StatType.DamageScale && mods[i].Op == ModifierOp.MultiplyCompound) scale /= mods[i].Value;
            }
        }
        return scale;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Rime;
        TriggerCooldown(ctx);

        _activeDebuffs++;
        hero.Auras.Apply(WintersEmbrace, ctx.CurrentTime);
        target?.Auras.Apply(_debuff, ctx.CurrentTime);

        PeriodicEffect.BeginHasted(ctx, hero, Name, DurationSeconds + _bonusDurationSeconds, PulsePeriodSeconds, tickOnStart: false, partialFinalTick: true,
            // Combat logs (10 Rime logs, 526 debuffs): the final pulse deals the fraction of a pulse period left when the
            // debuff runs out (leftover 0.4 -> 0.43x, 0.5 -> 0.52x, 0.8 -> 0.78x, 0.9 -> 0.88x of a full pulse).
            onTick: (c, fraction) => DealPulse(c, target, fraction),
            onEnd: (c, _) =>
            {
                if (--_activeDebuffs == 0) hero.Auras.Remove(WintersEmbrace, c.CurrentTime);
            },
            damageLogId: DamageLogId);
    }

    public void ResetState() => _activeDebuffs = 0;
}
