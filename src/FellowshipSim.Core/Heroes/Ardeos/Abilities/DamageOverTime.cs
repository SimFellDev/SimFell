namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// One of Ardeos's fire damage over time effects: its ticks' damage (logged under the effect's ID), period, duration,
/// stacking and Cinders. <see cref="DotTracker"/> keeps its instances on enemies and runs their ticks.
/// </summary>
public abstract class DamageOverTime : Ability
{
    protected DamageOverTime(Ardeos owner, int effectId, string name, double periodSeconds, double durationSeconds, DotStacking stacking)
        : base(owner, LogIds.Effect(effectId), name)
    {
        BasePeriod = periodSeconds;
        Duration = durationSeconds;
        Stacking = stacking;
        // Combat logs (yMAZVJL9hTpFjqbC fight 7): Fire Ball's and Fire Frogs' effects tick every 2s and 3s whatever the
        // haste (only Wildfire speeds them up); the others tick faster with haste.
        TicksScaleWithHaste = stacking != DotStacking.Accumulate;
        Debuff = AuraDef.Effect(name, effectId, maxStacks: 99);
        AplSlug = APL.AplNames.Slug(name);
        owner.AddDot(this);
    }

    /// <summary>The effect's action-list name ("engulfing_flames").</summary>
    public string AplSlug { get; }

    /// <summary>Seconds between ticks before haste and tick rate bonuses.</summary>
    public double BasePeriod { get; }

    /// <summary>Seconds an application lasts (talents add to it).</summary>
    public double Duration { get; set; }

    public DotStacking Stacking { get; }

    /// <summary>Ticks come faster with haste (and always with tick rate bonuses).</summary>
    public bool TicksScaleWithHaste { get; protected set; }

    /// <summary>Full ticks in one application at the base tick rate.</summary>
    public double BaseTicks => Duration / BasePeriod;

    /// <summary>Stacks a <see cref="DotStacking.Stacking"/> effect reaches.</summary>
    public int MaxStacks { get; protected set; } = 1;

    /// <summary>Cinders a full tick generates at the base tick rate (a faster tick generates less, the same per second).</summary>
    public double CindersPerTick { get; protected set; }

    /// <summary>False for effects whose damage over time cannot critically strike (Fire Ball, Fire Frogs).</summary>
    public bool TicksCanCrit { get; set; } = true;

    /// <summary>Whether the last tick of an application deals the part of a tick its time allows.</summary>
    public bool PartialFinalTick { get; protected set; } = true;

    /// <summary>The effect on an enemy, for action lists (stacks: its instances or stacks).</summary>
    public AuraDef Debuff { get; }

    /// <summary>Crit chance added to a tick as it goes off (Spontaneous Combustion).</summary>
    public List<Func<SimulationContext, DotInstance, double>> TickCritChanceBonuses { get; } = new();

    /// <summary>A tick went off (after its damage), with the tick's fraction of a full tick.</summary>
    public event Action<SimulationContext, DotInstance, double>? Ticked;

    private Ardeos Ardeos => (Ardeos)Owner;

    /// <summary>The damage multiplier of an instance's stacks.</summary>
    public virtual double StackMultiplier(DotInstance dot) => 1.0;

    /// <summary>Damage of one full tick of <paramref name="dot"/> now, before crit and spread.</summary>
    public virtual double TickDamage(SimulationContext ctx, DotInstance dot) =>
        Stacking == DotStacking.Accumulate ? dot.TickAmount : BaseDamage(ctx, dot.Target, StackMultiplier(dot));

    /// <summary>Damage <paramref name="dot"/> deals per second now, before crit and spread (Detonate).</summary>
    public double DamagePerSecond(SimulationContext ctx, DotInstance dot) =>
        TickDamage(ctx, dot) * Ardeos.Dots.Rate(this, ctx.CurrentTime) / BasePeriod;

    internal void Tick(SimulationContext ctx, DotInstance dot, double fraction)
    {
        var now = ctx.CurrentTime;
        var target = dot.Target;
        if (CindersPerTick > 0) Ardeos.GainCinders(TickCinders(ctx, dot, fraction), ctx, target);
        double extraCrit = 0.0;
        foreach (var bonus in TickCritChanceBonuses) extraCrit += bonus(ctx, dot);
        if (Stacking == DotStacking.Accumulate)
        {
            double amount = dot.TickAmount * fraction;
            double critChance = Owner.CritChanceAgainst(target, now) + CritChanceBonus + extraCrit;
            double critMultiplier = CritMultiplierAt(now);
            ctx.ApplyDamage(Owner, target, DamageLogId, Name, TicksCanCrit, _ => new SimulationContext.HitNumbers(amount, critChance, critMultiplier));
        }
        else
        {
            DealDamage(ctx, target, damageMultiplier: fraction * StackMultiplier(dot), canCrit: TicksCanCrit, extraCritChance: extraCrit);
        }
        dot.Ticks++;
        Ticked?.Invoke(ctx, dot, fraction);
    }

    /// <summary>Cinders of a tick: a faster tick rate generates the same Cinders per second.</summary>
    protected virtual double TickCinders(SimulationContext ctx, DotInstance dot, double fraction) =>
        CindersPerTick * dot.CinderScale * fraction / Ardeos.Dots.Rate(this, ctx.CurrentTime);

    /// <summary>Damage over time effects are applied by abilities, never cast.</summary>
    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
