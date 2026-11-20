namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 155 (weapon power 349, debuff effect 68): "Apply Voidbringer's Touch to target enemy for a time, causing a share of all
/// damage you deal to any enemy to be replicated and stored in the effect. Once Voidbringer's Touch expires, or a total
/// Main Stat damage cap has been accumulated, the effect erupts in a shadowy explosion, instantly dealing the
/// accumulated damage to the target. Voidbringer's Touch has increased critical strike chance. ... always triggers if the
/// damage it holds is greater than the remaining health of the enemy ... Can be cast during Global Cooldown."
/// Hitting the cap erupts at once; the eruption's crit chance is yours plus the bonus (the overflow is a grievous crit) (per the
/// user). UNVERIFIED: two of six eruptions in n7zxK8harZT2wjDB fight 9 come earlier in the log
/// than the engine's cap allows.
/// </summary>
public partial class VoidbringersTouch : Ability
{
    public const int DebuffEffectId = 68;
    public const double CritChance = 1.0;

    /// <summary>The debuff on the touched enemy while it stores damage (effect 68, "Voidbringer's Touch").</summary>
    public static readonly AuraDef Debuff = AuraDef.Effect(AbilityName, DebuffEffectId, AbilityDuration);

    private EnemyTarget? _target;
    private double _stored;
    private int _touch;
    private SimulationContext? _fight;

    public VoidbringersTouch(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.MaxDamageCoefficient;
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        owner.OnDamageDealt += (in dmg) =>
        {
            // A touch from an earlier fight stores nothing in this one.
            if (_target == null || dmg.AbilityId == DamageLogId || dmg.SimContext is not { } ctx || ctx != _fight) return;
            _stored += Constants.AccumulativeDamageFactor * dmg.Amount;
            // It erupts on reaching the cap, or as soon as it holds more than the enemy's remaining health.
            if (_stored >= Cap(ctx) || (_target.MaxHealth > 0 && _stored >= _target.CurrentHealth)) Erupt(ctx);
        };
    }

    private double Cap(SimulationContext ctx) =>
        Constants.MaxDamageCoefficient * Owner.PrimaryAttribute(ctx.CurrentTime);

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
        target.Auras.Apply(Debuff, ctx.CurrentTime);
        _stored = 0.0;
        int touch = ++_touch;
        ctx.Schedule(SimTime.FromSeconds(AbilityDuration), c => { if (touch == _touch) Erupt(c); }, Name);
    }

    /// <summary>
    /// The explosion: the cap's damage (the cap as Main Stat, with Martial Initiative and other Main Stat buffs) scaled by your
    /// damage buffs (Winter's Embrace, Ice Blitz, ...) and weapon bonuses but not Expertise, then a crit with all your crit
    /// chance plus the bonus, the overflow a grievous crit; short of the cap, the share of it stored. Combat logs
    /// (n7zxK8harZT2wjDB fight 9): the four eruptions that match in time match the log's damage closely; with
    /// Expertise they were too high.
    /// </summary>
    private void Erupt(SimulationContext ctx)
    {
        if (_target is not { } target) return;
        target.Auras.Remove(Debuff, ctx.CurrentTime);
        double share = Math.Min(1.0, _stored / Cap(ctx));
        _target = null;
        _stored = 0.0;
        _touch++;
        if (share <= 0.0) return;
        // Crit chance: all of yours plus the bonus; the overflow is a grievous crit (per the user).
        DealDamage(ctx, target, damageMultiplier: share, extraCritChance: CritChance);
    }
}
