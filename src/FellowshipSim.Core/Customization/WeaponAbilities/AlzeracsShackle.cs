namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 152 (weapon power 348): "Instantly Freeze target enemy in a block of ice for 3 seconds, incapacitating
/// them for the duration. While frozen, the target takes 174% frost damage every 1.5 seconds for the duration. Upon
/// Expiration, the target erupts in an explosion of frost magic, instantly dealing 63% frost damage to all enemies in an
/// 800 radius and Chilling them, reducing their Movement Speed by 65% and their Attack Speed by 40% for 12 seconds. While
/// Chilled, the afflicted enemies take 25.2% frost damage every 2 seconds for the duration. Can be cast during Global
/// Cooldown." Cooldown 30s; the explosion falls off as sqrt(8/N) beyond 8 enemies (game data targetCountFalloff).
/// Combat logs (1cAfr4DvZKzMqV76 fight 10 and five other Xavian fights, Xavian):
/// - Freeze (effect 63) lands 0.135-0.202s after the cast and lasts 3.007s. It ticks every 1.5s shortened by Haste, with a
///   partial final tick as it ends.
/// - The explosion is logged as 152, on every enemy, the frozen one included, as Freeze ends.
/// - Slow (effect 65, "Al'zerac's Shackle: Slow") lasts 15s (the effect record's duration; the tooltip's 12 is wrong) and
///   ticks every 2s shortened by Haste, with a partial final tick. A new explosion on a slowed enemy restarts the
///   duration and the tick period, with no tick at the refresh (36 refreshes: the next tick comes one period after it).
/// - Freeze and Slow ticks have the weapon ability's crit chance bonus (Brave Machinations): over the 19 Xavian fights,
///   Shackle hits crit 79-96% with Brave Machinations at rank 4, 55-63% at rank 1, and 31% without it.
/// - Freeze and Slow take their damage, crit chance and crit power when applied, and the enemy's multipliers at each
///   tick. Slow ticks hold their size when a damage buff ends or starts during the Slow: Meiko's, as Twin Souls: Army of
///   One (+20%) falls, 1.005 of the ticks before (196 Slows) and, as it rises, 0.993 (73 Slows); Helena's, as Martial
///   Initiative falls, 0.992 (2,490 Slows). UNVERIFIED: Freeze's numbers taken at the application (Meiko's Freezes
///   with Army of One changing during them: 1.051 of the ticks before as it falls, 2 Freezes; 0.994 as it rises, 3
///   Freezes), and crit chance taken at the application.
/// - Freeze and Slow keep ticking on an enemy after its death until they run out (effects 63 and 65 have no
///   RemoveOnDeath tag): over every tank fight, 23,007 of 208,854 Slow ticks and 117 of 8,722 Freeze ticks hit an enemy
///   after its logged death. An enemy that dies frozen gives no explosion (60 of 74 such Freezes; in the other 14 the
///   enemy died in the server frame of the explosion).
/// </summary>
public partial class AlzeracsShackle : Ability
{
    /// <summary>The explosion, of the primary stat. Game data: tuning <c>Aoe.PrimaryAttributeCoefficient</c> of weapon power 348.</summary>
    public const double Coefficient = 0.63;
    public const double TargetThreshold = 8;
    /// <summary>Combat logs: Freeze lands 4-6 server frames after the cast, mostly 5 (0.168s).</summary>
    public const double FreezeLandSeconds = 0.168;

    public const int FreezeEffectId = 63;
    public const string FreezeName = "Al'zerac's Shackle: Freeze";
    /// <summary>Game data: tuning <c>Stun.Dot.PrimaryAttributeCoefficient</c> of weapon power 348.</summary>
    public const double FreezeCoefficient = 1.74;
    /// <summary>Game data: tuning <c>Stun.Dot.TickInterval</c> of weapon power 348.</summary>
    public const double FreezeTickSeconds = 1.5;

    public const int SlowEffectId = 65;
    public const string SlowName = "Al'zerac's Shackle: Slow";
    /// <summary>Game data: tuning <c>Debuff.Dot.PrimaryAttributeCoefficient</c> of weapon power 348.</summary>
    public const double SlowCoefficient = 0.252;
    /// <summary>Game data: the duration of effect 65 (the tooltip has 12 seconds).</summary>
    public const double SlowSeconds = 15.0;
    /// <summary>Game data: tuning <c>Debuff.Dot.TickInterval</c> of weapon power 348.</summary>
    public const double SlowTickSeconds = 2.0;

    /// <summary>Freeze on the target.</summary>
    public static readonly AuraDef Freeze = AuraDef.Effect(FreezeName, FreezeEffectId, Constants.StunDuration);

    /// <summary>Slow on each enemy the explosion hit.</summary>
    public static readonly AuraDef Slow = AuraDef.Effect(SlowName, SlowEffectId, SlowSeconds);

    private readonly Tick _freezeTick;
    private readonly Tick _slowTick;

    /// <summary>The Freeze and Slow ticking on each enemy.</summary>
    private readonly Dictionary<EnemyTarget, PeriodicEffect> _freezes = new();
    private readonly Dictionary<EnemyTarget, PeriodicEffect> _slows = new();
    /// <summary>The fight the effects belong to (each sim iteration and replay has its own).</summary>
    private SimulationContext? _fight;

    public AlzeracsShackle(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        _freezeTick = new Tick(owner, FreezeEffectId, FreezeName, FreezeCoefficient);
        _slowTick = new Tick(owner, SlowEffectId, SlowName, SlowCoefficient);
        // Effects 63 and 65 are tagged EffectType.Dot.
        owner.DotDamageIds.Add(_freezeTick.DamageLogId);
        owner.DotDamageIds.Add(_slowTick.DamageLogId);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (_fight != ctx)
        {
            _fight = ctx;
            _freezes.Clear();
            _slows.Clear();
        }
        ctx.Schedule(SimTime.FromSeconds(FreezeLandSeconds), c => { if (!target.IsSleeping) Frost(c, target); }, FreezeName);
    }

    private void Frost(SimulationContext ctx, EnemyTarget target)
    {
        if (_freezes.Remove(target, out var old)) old.Stop(ctx);
        target.Auras.Apply(Freeze, ctx.CurrentTime);
        var numbers = _freezeTick.Take(ctx, CritChanceBonus);
        PeriodicEffect? freeze = null;
        freeze = PeriodicEffect.BeginHasted(ctx, Owner, FreezeName, Constants.StunDuration, FreezeTickSeconds, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) => _freezeTick.Deal(c, target, numbers, fraction),
            onEnd: (c, clipped) =>
            {
                if (_freezes.GetValueOrDefault(target) == freeze) _freezes.Remove(target);
                target.Auras.Remove(Freeze, c.CurrentTime);
                if (!clipped && !target.IsSleeping) Explode(c, target);
            },
            damageLogId: _freezeTick.DamageLogId, target: target, damageOverTime: true);
        freeze.TicksAfterTargetDies = true;
        _freezes[target] = freeze;
    }

    /// <summary>The explosion as Freeze ends: every enemy takes the hit and gets Slow.</summary>
    private void Explode(SimulationContext ctx, EnemyTarget target)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && !target.IsSleeping ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        foreach (var enemy in struck)
        {
            DealDamage(ctx, enemy, damageMultiplier: falloff);
            Chill(ctx, enemy);
        }
    }

    /// <summary>Slow on <paramref name="enemy"/>; on an enemy that has it, the duration and the tick period restart.</summary>
    private void Chill(SimulationContext ctx, EnemyTarget enemy)
    {
        if (_slows.Remove(enemy, out var old)) old.Stop(ctx);
        enemy.Auras.Apply(Slow, ctx.CurrentTime);
        var numbers = _slowTick.Take(ctx, CritChanceBonus);
        PeriodicEffect? slow = null;
        slow = PeriodicEffect.BeginHasted(ctx, Owner, SlowName, SlowSeconds, SlowTickSeconds, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) => _slowTick.Deal(c, enemy, numbers, fraction),
            onEnd: (c, clipped) =>
            {
                if (_slows.GetValueOrDefault(enemy) != slow) return;
                _slows.Remove(enemy);
                enemy.Auras.Remove(Slow, c.CurrentTime);
            },
            damageLogId: _slowTick.DamageLogId, target: enemy, damageOverTime: true);
        slow.TicksAfterTargetDies = true;
        _slows[enemy] = slow;
    }

    /// <summary>The damage, crit chance and crit power of one Freeze or Slow, taken when it is applied.</summary>
    private sealed record Numbers(double Damage, double CritChance, double CritMultiplier);

    /// <summary>The damage numbers of a Freeze or Slow tick (logged as its effect).</summary>
    private sealed class Tick : Ability
    {
        public Tick(Hero owner, int effectId, string name, double coefficient)
            : base(owner, LogIds.Effect(effectId), name, AbilityCategory)
        {
            BaseCoefficient = coefficient;
        }

        /// <summary>The numbers now, with the weapon ability's crit chance bonus.</summary>
        public Numbers Take(SimulationContext ctx, double weaponCritChanceBonus)
        {
            var now = ctx.CurrentTime;
            return new Numbers(BaseDamage(ctx, null), Owner.CritChance(now) + CritChanceBonus + weaponCritChanceBonus, CritMultiplierAt(now));
        }

        /// <summary>A tick with the numbers taken at the application, and the enemy's multipliers at the tick.</summary>
        public void Deal(SimulationContext ctx, EnemyTarget enemy, Numbers numbers, double fraction)
        {
            var now = ctx.CurrentTime;
            ctx.ApplyDamage(Owner, enemy, numbers.Damage * fraction * Owner.DamageMultiplierAgainst(enemy, now), DamageLogId, Name, canCrit: true,
                numbers.CritChance + Owner.CritChanceAgainst(enemy, now) - Owner.CritChance(now), numbers.CritMultiplier, snapshot: true);
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
