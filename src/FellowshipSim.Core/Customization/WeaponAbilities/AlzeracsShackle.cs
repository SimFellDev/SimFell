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
/// </summary>
public class AlzeracsShackle : Ability
{
    public const int AbilityId = 152;
    public const string AbilityName = "Al'zerac's Shackle";
    /// <summary>The explosion, of the primary stat.</summary>
    public const double Coefficient = 0.63;
    public const double CooldownSeconds = 30.0;
    public const double TargetThreshold = 8;
    /// <summary>Combat logs: Freeze lands 4-6 server frames after the cast, mostly 5 (0.168s).</summary>
    public const double FreezeLandSeconds = 0.168;

    public const int FreezeEffectId = 63;
    public const string FreezeName = "Al'zerac's Shackle: Freeze";
    public const double FreezeCoefficient = 1.74;
    public const double FreezeSeconds = 3.0;
    public const double FreezeTickSeconds = 1.5;

    public const int SlowEffectId = 65;
    public const string SlowName = "Al'zerac's Shackle: Slow";
    public const double SlowCoefficient = 0.252;
    public const double SlowSeconds = 15.0;
    public const double SlowTickSeconds = 2.0;

    /// <summary>Freeze on the target.</summary>
    public static readonly AuraDef Freeze = AuraDef.Effect(FreezeName, FreezeEffectId, FreezeSeconds);

    /// <summary>Slow on each enemy the explosion hit.</summary>
    public static readonly AuraDef Slow = AuraDef.Effect(SlowName, SlowEffectId, SlowSeconds);

    private readonly Tick _freezeTick;
    private readonly Tick _slowTick;

    /// <summary>The Freeze and Slow ticking on each enemy.</summary>
    private readonly Dictionary<EnemyTarget, PeriodicEffect> _freezes = new();
    private readonly Dictionary<EnemyTarget, PeriodicEffect> _slows = new();
    /// <summary>The fight the effects belong to (each sim iteration and replay has its own).</summary>
    private SimulationContext? _fight;

    public AlzeracsShackle(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        SetCooldown(CooldownSeconds);
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
            ctx.EnemyDemising += (c, enemy) =>
            {
                if (_freezes.Remove(enemy, out var freeze)) freeze.Stop(c);
                if (_slows.Remove(enemy, out var slow)) slow.Stop(c);
            };
        }
        ctx.Schedule(SimTime.FromSeconds(FreezeLandSeconds), c => { if (!target.IsSleeping) Frost(c, target); }, FreezeName);
    }

    private void Frost(SimulationContext ctx, EnemyTarget target)
    {
        if (_freezes.Remove(target, out var old)) old.Stop(ctx);
        target.Auras.Apply(Freeze, ctx.CurrentTime);
        PeriodicEffect? freeze = null;
        freeze = PeriodicEffect.BeginHasted(ctx, Owner, FreezeName, FreezeSeconds, FreezeTickSeconds, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) => _freezeTick.DealDamage(c, target, damageMultiplier: fraction, extraCritChance: CritChanceBonus),
            onEnd: (c, clipped) =>
            {
                if (_freezes.GetValueOrDefault(target) == freeze) _freezes.Remove(target);
                target.Auras.Remove(Freeze, c.CurrentTime);
                if (!clipped) Explode(c, target);
            },
            damageLogId: _freezeTick.DamageLogId, target: target, damageOverTime: true);
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
        PeriodicEffect? slow = null;
        slow = PeriodicEffect.BeginHasted(ctx, Owner, SlowName, SlowSeconds, SlowTickSeconds, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) => _slowTick.DealDamage(c, enemy, damageMultiplier: fraction, extraCritChance: CritChanceBonus),
            onEnd: (c, clipped) =>
            {
                if (_slows.GetValueOrDefault(enemy) != slow) return;
                _slows.Remove(enemy);
                enemy.Auras.Remove(Slow, c.CurrentTime);
            },
            damageLogId: _slowTick.DamageLogId, target: enemy, damageOverTime: true);
        _slows[enemy] = slow;
    }

    /// <summary>The damage numbers of a Freeze or Slow tick (logged as its effect).</summary>
    private sealed class Tick : Ability
    {
        public Tick(Hero owner, int effectId, string name, double coefficient)
            : base(owner, LogIds.Effect(effectId), name, AbilityCategory.Weapon)
        {
            BaseCoefficient = coefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
