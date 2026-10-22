namespace FellowshipSim.Core.Domain;

/// <summary>An ability's category in the game (it decides which talents, gear and rules apply to it).</summary>
public enum AbilityCategory
{
    None,
    Basic,
    Core,
    Power,
    Major,
    Spirit,
    Weapon,
    Relic,
    Defensive,
    Utility,
    Control,
    Movement,
}

/// <summary>
/// An ability of a hero. Each ability class lists its game values as constants at the top (ID, name, coefficient,
/// cooldown, cast time...) and passes them in its constructor; <see cref="Execute"/> is what it does.
/// </summary>
public abstract class Ability
{
    public int Id { get; }
    public string Name { get; }
    public Hero Owner { get; }
    public AbilityCategory Category { get; }
    public double BaseCoefficient { get; protected set; }
    public SimTime CastTime { get; protected set; }
    public SimTime Cooldown { get; protected set; }
    public int MaxCharges { get; protected set; } = 1;
    public bool IgnoresGcd { get; protected set; } = false;

    /// <summary>The ID combat logs use for this ability's damage (the ability ID unless it deals damage through an effect).</summary>
    public int DamageLogId { get; protected set; }

    /// <summary>Talent and gear multipliers on this ability's damage.</summary>
    public double DamageMultiplier { get; protected set; } = 1.0;

    /// <summary>Extra crit chance on this ability's hits.</summary>
    public double CritChanceBonus { get; set; } = 0.0;

    /// <summary>Multiplier on this ability's crit power (e.g. "20% increased Critical Strike power").</summary>
    public double CritPowerMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Main Stat damage added to each of this ability's hits after everything else (no expertise, damage buffs or crit),
    /// as a coefficient (e.g. The Vainglorious).
    /// </summary>
    public double BonusCoefficient { get; set; } = 0.0;

    public bool ScalesWithExpertise { get; protected set; } = true;

    /// <summary>The cooldown is divided by (1 + haste) once, when it starts (game data "cooldownTimeRateScalesOff" lists Haste, single charge).</summary>
    public bool CooldownScalesWithHaste { get; protected set; } = false;

    /// <summary>
    /// Charges recharge with "Cooldown Acceleration equal to your Haste": haste adds to the recovery rate while recharging
    /// (game data "cooldownTimeRateScalesOff" lists Haste, on an ability with charges).
    /// </summary>
    public bool ChargesRechargeWithHaste { get; protected set; } = false;

    /// <summary>Weapon abilities run outside cooldown recovery and haste (only cooldown reduction and weapon-specific effects).</summary>
    public bool IgnoresCooldownRecovery => Category == AbilityCategory.Weapon;

    /// <summary>Log IDs of the ability's own auras whose cooldown recovery speeds up its own cooldown too.</summary>
    public HashSet<int> OwnRecoveryAuraLogIds { get; } = new();

    /// <summary>Channeled abilities tick for <see cref="ChannelDuration"/> after their cast resolves.</summary>
    public bool IsChanneled { get; protected set; } = false;
    public SimTime ChannelDuration { get; protected set; } = SimTime.Zero;
    /// <summary>Unhasted seconds between channel ticks; haste at channel start shortens it.</summary>
    public double ChannelTickPeriodSeconds { get; protected set; } = 1.0;
    public bool ChannelTicksOnStart { get; protected set; } = false;
    public bool ChannelPartialFinalTick { get; protected set; } = false;

    /// <summary>Unhasted seconds between ticks of the channel starting now (abilities whose tick rate a buff changes override this).</summary>
    public virtual double ChannelTickPeriodFor(SimulationContext ctx) => ChannelTickPeriodSeconds;

    /// <summary>
    /// Multi-target damage scaling: when a hit lands on more than this many targets, each takes sqrt(T/N) of the damage
    /// (0 = no scaling).
    /// </summary>
    public double AoeTargetThreshold { get; protected set; } = 0.0;

    /// <summary>True when the ability's hits keep the damage they had on release instead of reading stats when they land.</summary>
    public bool SnapshotsDamage { get; protected set; }

    /// <summary>
    /// One of this ability's hits landed (under <see cref="DamageLogId"/>), with its target, damage and whether it crit.
    /// Talents and other abilities that react to this ability's hits subscribe here.
    /// </summary>
    public event Action<DamageContext>? Hit;

    /// <summary>What talents added to the cast resolving now (<see cref="ResolveCast(SimulationContext, EnemyTarget, object?)"/>); null outside a cast.</summary>
    protected CastBonus? CurrentCast { get; private set; }

    /// <summary>The damage multiplier talents added to the cast resolving now (1 outside a cast).</summary>
    protected double CastDamageMultiplier => CurrentCast?.DamageMultiplier ?? 1.0;

    protected Ability(Hero owner, int id, string name, AbilityCategory category = AbilityCategory.None)
    {
        Owner = owner;
        Id = id;
        DamageLogId = id;
        Name = name;
        Category = category;
        owner.OnDamageDealt += dmg =>
        {
            if (dmg.AbilityId == DamageLogId) Hit?.Invoke(dmg);
        };
    }

    /// <summary>
    /// Called once the hero's abilities all exist (before talents and gear bind): an ability that reacts to another
    /// ability (e.g. to its <see cref="Hit"/>) subscribes here.
    /// </summary>
    public virtual void Connect() { }

    /// <summary>
    /// Sets the cooldown and charges. With <paramref name="scalesWithHaste"/> (game data "cooldownTimeRateScalesOff" lists
    /// Haste; for Rime only Cold Snap), a single-charge cooldown is divided by haste when it starts and charges recharge
    /// with haste added to their rate.
    /// Weapon abilities ignore haste.
    /// </summary>
    protected void SetCooldown(double seconds, int charges = 1, bool scalesWithHaste = false)
    {
        Cooldown = SimTime.FromSeconds(seconds);
        MaxCharges = charges;
        bool hasted = seconds > 0 && scalesWithHaste && Category != AbilityCategory.Weapon;
        ChargesRechargeWithHaste = hasted && charges > 1;
        CooldownScalesWithHaste = hasted && charges <= 1;
    }

    /// <summary>Makes the ability a channel of <paramref name="durationSeconds"/>, ticking every <paramref name="tickPeriodSeconds"/> (hasted).</summary>
    protected void SetChannel(double durationSeconds, double tickPeriodSeconds)
    {
        IsChanneled = true;
        ChannelDuration = SimTime.FromSeconds(durationSeconds);
        ChannelTickPeriodSeconds = tickPeriodSeconds;
    }

    public void AddDamageMultiplier(double multiplier) => DamageMultiplier *= multiplier;
    public void ResetDamageMultiplier() => DamageMultiplier = 1.0;

    public bool IsInstant => CastTime == SimTime.Zero;

    public virtual SimTime GetAdjustedCastTime(SimTime now = default)
    {
        if (IsInstant) return SimTime.Zero;
        return SimTime.FromSeconds(CastTime.TotalSeconds / (1.0 + Owner.GetHaste(now)));
    }

    public bool IsReady(SimTime now) => Owner.IsCooldownReady(Name, now);

    /// <summary>Whether the ability can be cast right now.</summary>
    public bool CanCast(SimulationContext ctx) => CanCast(ctx, explain: false, out _);

    /// <summary>Whether the ability can be cast right now, and the reason when it cannot.</summary>
    public bool CanCast(SimulationContext ctx, out string? reason) => CanCast(ctx, explain: true, out reason);

    /// <summary>
    /// Whether the ability can be cast right now. The default checks charges/cooldown;
    /// abilities with resource costs or other requirements add their own checks.
    /// </summary>
    /// <param name="explain">Build <paramref name="reason"/>. Action lists check every decision and discard the reason.</param>
    protected virtual bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (Cooldown > SimTime.Zero && Owner.CooldownOf(this).ChargesAt(ctx.CurrentTime) == 0)
        {
            reason = explain ? $"{Name} on cooldown ({Owner.GetCooldownRemains(Name, ctx.CurrentTime):F2}s remaining)" : null;
            return false;
        }
        reason = null;
        return true;
    }

    /// <summary>Spends a charge and starts its recharge.</summary>
    public virtual void TriggerCooldown(SimulationContext ctx, double cooldownMultiplier = 1.0)
    {
        if (Cooldown <= SimTime.Zero) return;
        // Cooldown reduction cuts the timer once, at cast; hasted cooldowns are divided by haste at cast too.
        // Cooldown recovery (and haste for charge abilities) then sets how fast it ticks down.
        double seconds = Cooldown.TotalSeconds * cooldownMultiplier * (1.0 - Owner.Stats.CooldownReductionPct);
        if (CooldownScalesWithHaste) seconds /= 1.0 + Owner.GetHaste(ctx.CurrentTime);
        Owner.CooldownOf(this).Spend(ctx.CurrentTime, seconds);
    }

    /// <summary>The ability's effects when its cast resolves (immediately for instants, at cast completion otherwise).</summary>
    public abstract void Execute(SimulationContext ctx, EnemyTarget target);

    /// <summary>
    /// Resolves a cast: talents first add to it (<see cref="Hero.OnCastResolving"/>), then <see cref="Execute"/> runs and
    /// its hits get what they added. The engine resolves every cast this way; a proc that casts an ability for free passes
    /// itself as <paramref name="source"/>.
    /// </summary>
    public void ResolveCast(SimulationContext ctx, EnemyTarget target, object? source = null) =>
        ResolveCast(ctx, target, source, () => Execute(ctx, target));

    /// <summary>Resolves <paramref name="effect"/> as a cast of this ability (a free cast that does less than <see cref="Execute"/>).</summary>
    protected void ResolveCast(SimulationContext ctx, EnemyTarget target, object? source, Action effect)
    {
        var outer = CurrentCast;
        CurrentCast = Owner.PrepareCast(this, target, ctx.CurrentTime, source);
        try
        {
            effect();
        }
        finally
        {
            CurrentCast = outer;
        }
    }

    /// <summary>One channel tick. <paramref name="fraction"/> is below 1.0 for a partial final tick.</summary>
    public virtual void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction) { }

    /// <summary>The channel ended, naturally or because it was stopped early (<paramref name="clipped"/>).</summary>
    public virtual void OnChannelEnd(SimulationContext ctx, EnemyTarget target, bool clipped) { }

    /// <summary>Damage of one hit before crit and spread.</summary>
    public virtual double BaseDamage(SimulationContext ctx, EnemyTarget? target, double damageMultiplier = 1.0)
    {
        var now = ctx.CurrentTime;
        double expertiseAmp = ScalesWithExpertise ? 1.0 + Owner.Expertise(now) : 1.0;
        return BaseCoefficient
               * Owner.PrimaryAttribute(now)
               * expertiseAmp
               * DamageScaleFor(ctx)
               * Owner.DamageMultiplierAgainst(target, now)
               * Owner.AbilityDamageMultiplier(this, now)
               * DamageMultiplier
               * damageMultiplier;
    }

    /// <summary>This ability's crit multiplier at <paramref name="now"/>.</summary>
    public double CritMultiplierAt(SimTime now) => Owner.CritMultiplier(now) * CritPowerMultiplier;

    /// <summary>Outgoing damage multiplier from auras for this ability (abilities excluded from some auras override this).</summary>
    protected virtual double DamageScaleFor(SimulationContext ctx) => Owner.DamageScale(ctx.CurrentTime);

    /// <param name="extraCritChance">Crit chance this hit alone gets (e.g. from a buff the cast consumed).</param>
    public virtual double DealDamage(SimulationContext ctx, EnemyTarget target, double damageMultiplier = 1.0, bool canCrit = true, string? overrideName = null, int? overrideId = null, bool? forcedCrit = null,
        Action<SimulationContext, double>? onLanded = null, double extraCritChance = 0.0)
    {
        // Damage, crit chance and crit power are read when the hit lands; what talents added to the cast is kept.
        double castCrit = CurrentCast?.CritChance ?? 0.0;
        double castDamage = CastDamageMultiplier;
        var hit = ctx.RentHit(Owner, target, overrideId ?? DamageLogId, overrideName ?? Name, canCrit, forcedCrit, onLanded, SnapshotsDamage);
        hit.FromAbility(this, damageMultiplier * castDamage, castCrit, extraCritChance);
        return ctx.ApplyDamage(hit);
    }

    public virtual double DealAoeDamage(SimulationContext ctx, IEnumerable<EnemyTarget> targets, double damageMultiplier = 1.0, bool canCrit = true, string? overrideName = null, int? overrideId = null, bool? forcedCrit = null,
        double extraCritChance = 0.0)
    {
        var targetList = ctx.Outcomes.SelectTargets(overrideId ?? DamageLogId, targets as IReadOnlyList<EnemyTarget> ?? targets.ToList(), ctx);
        if (targetList.Count == 0) return 0.0;

        double falloffMultiplier = 1.0;
        if (AoeTargetThreshold > 0.0 && targetList.Count > AoeTargetThreshold)
        {
            falloffMultiplier = Math.Sqrt(AoeTargetThreshold / targetList.Count);
        }

        double total = 0.0;
        foreach (var t in targetList)
        {
            total += DealDamage(ctx, t, damageMultiplier * falloffMultiplier, canCrit, overrideName, overrideId, forcedCrit, extraCritChance: extraCritChance);
        }
        return total;
    }
}
