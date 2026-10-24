namespace FellowshipSim.Core.Engine;

/// <summary>
/// One hit from its release to its landing: its source, target and damage ID, what the cast added to it, and its numbers at
/// a moment (<see cref="Compute"/>). Each <see cref="SimulationContext"/> keeps a pool of them; a hit with a travel time is
/// its own landing event (<see cref="SimulationKernel.ScheduleAt(SimTime, PendingHit, string)"/>).
/// </summary>
public sealed class PendingHit
{
    internal PendingHit() { }

    public Hero Source { get; private set; } = null!;
    public EnemyTarget? Target { get; private set; }
    public int AbilityId { get; private set; }
    public string AbilityName { get; private set; } = "";
    public bool CanCrit { get; private set; }
    public bool? ForcedCrit { get; private set; }
    /// <summary>The hit keeps the numbers it had on release instead of reading stats when it lands.</summary>
    public bool Snapshot { get; private set; }

    /// <summary>The ability whose damage formula gives the numbers (null for fixed or custom numbers).</summary>
    public Ability? Ability { get; private set; }
    /// <summary>The damage multiplier passed to <see cref="Domain.Ability.BaseDamage"/>: the hit's own times the cast's.</summary>
    public double DamageMultiplier { get; private set; }
    /// <summary>Crit chance the cast added (<see cref="CastBonus.CritChance"/>).</summary>
    public double CastCritChance { get; private set; }
    /// <summary>Crit chance this hit alone gets.</summary>
    public double ExtraCritChance { get; private set; }

    private SimulationContext.HitNumbers _fixed;
    private Func<SimulationContext, SimulationContext.HitNumbers>? _compute;

    internal Action<SimulationContext, double>? OnLanded { get; private set; }
    internal double TickFraction { get; set; }
    internal HitRequest Released { get; set; }
    internal HitOutcome Outcome { get; set; }
    /// <summary>The period timer whose tick released this hit of its own damage, when the hit lands later.</summary>
    internal HastedTimer? Tick { get; set; }

    internal void Begin(Hero source, EnemyTarget? target, int abilityId, string abilityName, bool canCrit, bool? forcedCrit,
        Action<SimulationContext, double>? onLanded, bool snapshot)
    {
        Source = source;
        Target = target;
        AbilityId = abilityId;
        AbilityName = abilityName;
        CanCrit = canCrit;
        ForcedCrit = forcedCrit;
        OnLanded = onLanded;
        Snapshot = snapshot;
    }

    /// <summary>The numbers come from <paramref name="ability"/>'s damage formula with the cast's bonuses kept.</summary>
    internal void FromAbility(Ability ability, double damageMultiplier, double castCritChance, double extraCritChance)
    {
        Ability = ability;
        DamageMultiplier = damageMultiplier;
        CastCritChance = castCritChance;
        ExtraCritChance = extraCritChance;
    }

    internal void FromNumbers(SimulationContext.HitNumbers numbers) => _fixed = numbers;

    internal void FromCompute(Func<SimulationContext, SimulationContext.HitNumbers> compute) => _compute = compute;

    /// <summary>The hit's numbers from the state at <paramref name="ctx"/>'s current time.</summary>
    public SimulationContext.HitNumbers Compute(SimulationContext ctx)
    {
        if (Ability is { } ability)
        {
            // Damage, crit chance and crit power are read now; what talents added to the cast is kept.
            var now = ctx.CurrentTime;
            var owner = ability.Owner;
            double hitCrit = 0.0;
            var bonuses = ability.HitCritChanceBonuses;
            for (int i = 0; i < bonuses.Count; i++) hitCrit += bonuses[i](ctx);
            return new SimulationContext.HitNumbers(
                ability.BaseDamage(ctx, Target, DamageMultiplier),
                owner.CritChanceAgainst(Target, now) + ability.CritChanceBonus + ExtraCritChance + CastCritChance + hitCrit,
                ability.CritMultiplierAt(now));
        }
        return _compute != null ? _compute(ctx) : _fixed;
    }

    /// <summary>Drops every reference, so a pooled hit keeps no hero, target or handler alive.</summary>
    internal void Clear()
    {
        Source = null!;
        Target = null;
        AbilityName = "";
        OnLanded = null;
        Ability = null;
        _compute = null;
        _fixed = default;
        Released = default;
        Outcome = default;
        Tick = null;
    }
}
