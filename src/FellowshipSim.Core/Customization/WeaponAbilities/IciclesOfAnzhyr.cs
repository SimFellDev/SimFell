namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 1561 (Frozen Staff of An'zhyr, weapon power 357): "Conjure waves of icicles to pelt the target area,
/// dealing Main Stat frost damage to all enemies in the area with each wave. Enemies hit by the final wave are
/// afflicted by Curse of An'zhyr permanently, taking Main Stat frost damage at an interval. Enemies afflicted by
/// Curse of An'zhyr take increased direct damage from Icicles of An'zhyr. Can be cast during Global Cooldown."
/// Combat logs (PCaMVTFbZzNLrfx3 fight 5, 12 casts): the waves land at fixed intervals after the cast and keep the
/// damage, crit chance and crit power of the cast; damage falls off beyond a target cap (every wave on 16-22
/// enemies fits one cap; all 12 casts then match). The curse (effect 2286) ticks on a hasted period.
/// </summary>
public partial class IciclesOfAnzhyr : Ability
{
    /// <summary>Game data: tuning <c>Aoe.Damage.PrimaryAttributeCoefficient</c> of weapon power 357.</summary>
    public const double Coefficient = 1.44;
    /// <summary>Combat logs: the first wave lands 0.27s after the cast (the game data's <see cref="Constants.AoeVolleyDamageDelay"/> is 0.25).</summary>
    public const double FirstWaveSeconds = 0.27;
    public const double TargetThreshold = 15;
    public const double CursedDamageMultiplier = 3.0;

    public const int CurseEffectId = 2286;
    public const string CurseName = "Curse of An'zhyr";
    /// <summary>Game data: tuning <c>Debuff.PrimaryAttributeCoefficient</c> of weapon power 357.</summary>
    public const double CurseCoefficient = 0.384;

    /// <summary>The curse on an enemy (permanent).</summary>
    public static readonly AuraDef Curse = AuraDef.Effect(CurseName, CurseEffectId);

    private readonly CurseTick _curse;

    public IciclesOfAnzhyr(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        _curse = new CurseTick(owner);
        // Effect 2286 is tagged EffectType.Dot.
        owner.DotDamageIds.Add(LogIds.Effect(CurseEffectId));
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (_fight != ctx)
        {
            _fight = ctx;
            _curses.Clear();
        }
        // The waves keep the damage, crit chance and crit power of the cast.
        var now = ctx.CurrentTime;
        double damage = BaseDamage(ctx, null);
        double critChance = Owner.CritChance(now) + CritChanceBonus;
        double critMultiplier = CritMultiplierAt(now);
        // The curse this cast applies (or re-applies) snapshots the cast too.
        var curse = new CurseSnapshot(_curse.BaseDamage(ctx, null), critChance, critMultiplier);
        for (int i = 0; i < Constants.AoeNumberOfVolleys; i++)
        {
            bool final = i == Constants.AoeNumberOfVolleys - 1;
            ctx.Schedule(SimTime.FromSeconds(FirstWaveSeconds + i * Constants.AoeVolleyInterval),
                c => Wave(c, target, final, damage, critChance, critMultiplier, curse), Name);
        }
    }

    /// <summary>The curse's numbers, taken when Icicles is cast.</summary>
    private sealed record CurseSnapshot(double Damage, double CritChance, double CritMultiplier);

    /// <summary>Each cursed enemy's current curse (a re-application replaces its snapshot).</summary>
    private readonly Dictionary<EnemyTarget, CurseSnapshot> _curses = new();
    /// <summary>The fight the curses belong to (each sim iteration and replay has its own).</summary>
    private SimulationContext? _fight;

    private void Wave(SimulationContext ctx, EnemyTarget target, bool final, double damage, double critChance, double critMultiplier, CurseSnapshot curse)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        foreach (var enemy in struck)
        {
            bool cursed = enemy.Auras.HasAura(Curse, ctx.CurrentTime);
            double multiplier = falloff * (cursed ? CursedDamageMultiplier : 1.0);
            var now = ctx.CurrentTime;
            ctx.ApplyDamage(Owner, enemy, damage * multiplier * Owner.DamageMultiplierAgainst(enemy, now), DamageLogId, Name, canCrit: true,
                critChance + Owner.CritChanceAgainst(enemy, now) - Owner.CritChance(now), critMultiplier, snapshot: true);
            if (final)
            {
                // Re-applying the curse gives it the new cast's snapshot; a new curse also starts ticking.
                bool ticking = _curses.ContainsKey(enemy) && cursed;
                _curses[enemy] = curse;
                if (!ticking) Afflict(ctx, enemy);
            }
        }
    }

    /// <summary>
    /// Curse of An'zhyr: permanent, ticking on a hasted period while the enemy lives. Each tick deals the snapshot of the
    /// Icicles cast that last applied it (per the user: every re-application is a new snapshot).
    /// </summary>
    private void Afflict(SimulationContext ctx, EnemyTarget enemy)
    {
        enemy.Auras.Apply(Curse, ctx.CurrentTime);
        HastedTimer? timer = null;
        void Tick(SimulationContext c)
        {
            if (enemy.IsSleeping || !_curses.TryGetValue(enemy, out var snap))
            {
                timer!.Cancel();
                return;
            }
            var now = c.CurrentTime;
            c.ApplyDamage(Owner, enemy, snap.Damage * Owner.DamageMultiplierAgainst(enemy, now), _curse.DamageLogId, CurseName, canCrit: true,
                snap.CritChance + Owner.CritChanceAgainst(enemy, now) - Owner.CritChance(now), snap.CritMultiplier, snapshot: true);
        }
        // Effect 2286 is tagged EffectType.Dot with no tickrate exemption, a hasted period, Infinite, no tick on application
        // Its ticks run on the game's period timer, which each Haste change re-arms at Period / Haste(now).
        timer = HastedTimer.StartPeriod(ctx, Owner, Constants.DebuffTickInterval, Tick, CurseName, _curse.DamageLogId);
    }

    /// <summary>The curse's damage numbers (logged as effect 2286).</summary>
    private sealed class CurseTick : Ability
    {
        public CurseTick(Hero owner) : base(owner, LogIds.Effect(CurseEffectId), CurseName, AbilityCategory)
        {
            BaseCoefficient = CurseCoefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
