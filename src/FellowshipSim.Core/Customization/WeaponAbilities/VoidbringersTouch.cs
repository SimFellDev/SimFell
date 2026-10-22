namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 155 (weapon power 349): "Apply Voidbringer's Touch to target enemy for 15 seconds, causing 10% of all
/// damage you deal to any enemy to be replicated and stored in the effect. Once Voidbringer's Touch expires, or a total of
/// 4250% Main Stat damage has been accumulated, the effect erupts in a shadowy explosion, instantly dealing the
/// accumulated damage to the target. Voidbringer's Touch has +100% critical strike chance. ... always triggers if the
/// damage it holds is greater than the remaining health of the enemy ... Can be cast during Global Cooldown."
/// Hitting the cap erupts at once; the eruption's crit chance is yours + 100% (the overflow is a grievous crit) (per the
/// user). Cooldown 90s. UNVERIFIED: two of six eruptions in n7zxK8harZT2wjDB fight 9 come 0.6-1.2s earlier in the log
/// than the engine's cap allows.
/// </summary>
public class VoidbringersTouch : Ability
{
    public const int AbilityId = 155;
    public const string AbilityName = "Voidbringer's Touch";
    public const double CooldownSeconds = 90.0;
    public const double DurationSeconds = 15.0;
    public const double StoredShare = 0.10;
    /// <summary>The cap on stored damage, as Main Stat.</summary>
    public const double CapCoefficient = 42.5;
    public const double CritChance = 1.0;

    private EnemyTarget? _target;
    private double _stored;
    private int _touch;
    private SimulationContext? _fight;

    public VoidbringersTouch(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = CapCoefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        IgnoresGcd = true;
        owner.OnDamageDealt += (in dmg) =>
        {
            // A touch from an earlier fight stores nothing in this one.
            if (_target == null || dmg.AbilityId == DamageLogId || dmg.SimContext is not { } ctx || ctx != _fight) return;
            _stored += StoredShare * dmg.Amount;
            // It erupts on reaching the cap, or as soon as it holds more than the enemy's remaining health.
            if (_stored >= Cap(ctx) || (_target.MaxHealth > 0 && _stored >= _target.CurrentHealth)) Erupt(ctx);
        };
    }

    private double Cap(SimulationContext ctx) =>
        CapCoefficient * Owner.PrimaryAttribute(ctx.CurrentTime);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (_fight != ctx)
        {
            _fight = ctx;
            _target = null;
            ctx.EnemyDemising += (c, enemy) => { if (enemy == _target) Erupt(c); };
        }
        if (_target != null) Erupt(ctx);
        _target = target;
        _stored = 0.0;
        int touch = ++_touch;
        ctx.Schedule(SimTime.FromSeconds(DurationSeconds), c => { if (touch == _touch) Erupt(c); }, Name);
    }

    /// <summary>
    /// The explosion: the cap's damage (42.5 x Main Stat, with Martial Initiative and other Main Stat buffs) scaled by your
    /// damage buffs (Winter's Embrace, Ice Blitz, ...) and weapon bonuses but not Expertise, then a crit with all your crit
    /// chance + 100%, the overflow a grievous crit; short of the cap, the share of it stored. Combat logs
    /// (n7zxK8harZT2wjDB fight 9): the four eruptions that match in time are 0.94-1.05 of the log (mean 0.99); with
    /// Expertise they were 1.37x too high.
    /// </summary>
    private void Erupt(SimulationContext ctx)
    {
        if (_target is not { } target) return;
        double share = Math.Min(1.0, _stored / Cap(ctx));
        _target = null;
        _stored = 0.0;
        _touch++;
        if (share <= 0.0) return;
        // Crit chance: all of yours + 100%; the overflow is a grievous crit (per the user).
        DealDamage(ctx, target, damageMultiplier: share, extraCritChance: CritChance);
    }
}
