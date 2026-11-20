namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 985: "Throw your shield at target enemy dealing 341.6% of Strength physical damage. The shield bounces to up to
/// 5 additional nearby enemies. Each time Shield Throw deals damage it generates Toughness relative to the damage dealt.
/// VETERAN OF WAR: Shield Throw reduces the cooldown of your Shockwave ability by 3.0 seconds." Cooldown 21s, shortened by
/// Haste at the cast. Combat logs (HrNZ6wQfFKGxybDR fight 1 @ 16.027): the first hit lands 0.33s after the cast and the
/// bounces 0.03-0.2s apart, each bounce dealing what the first hit deals.
/// </summary>
public partial class ShieldThrow : HelenaAbility
{
    /// <summary>Combat logs (HrNZ6wQfFKGxybDR fight 1, v8rhNxVc3PwDqzBA fight 4): the first hit lands 0.27-0.37s after the cast.</summary>
    public const double FirstHitSeconds = 0.3;
    public const double ShockwaveReductionSeconds = 3.0;

    /// <summary>
    /// A bounce to one more enemy. A sim bounces to every enemy up to <see cref="Constants.MaxNumBounces"/>; a replay bounces as often as
    /// the log shows.
    /// </summary>
    public const string BounceProcKey = "shield_throw_bounce";

    /// <summary>The first hit on one enemy: a sim takes the cast's target, a replay the log's first hit.</summary>
    public const string FirstHitProcKey = "shield_throw_first_hit";

    /// <summary>Multipliers on the first hit alone (Skull Cracker).</summary>
    public List<Func<SimulationContext, double>> FirstHitMultipliers { get; } = new();

    /// <summary>Raised as the first hit lands, with its enemy and the damage it dealt (Razor Crash).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? FirstHitLanded;

    /// <summary>Raised as the cast resolves (Martial Command, The Best Defense).</summary>
    public event Action<SimulationContext>? Thrown;

    public ShieldThrow(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        VeteranOfWarReductions.Add((typeof(Shockwave), ShockwaveReductionSeconds));
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        double castMultiplier = CastDamageMultiplier;
        double castCrit = CurrentCast?.CritChance ?? 0.0;
        double firstMultiplier = 1.0;
        foreach (var multiplier in FirstHitMultipliers) firstMultiplier *= multiplier(ctx);
        ctx.Schedule(SimTime.FromSeconds(FirstHitSeconds), c =>
        {
            if (FirstTarget(c, target) is not { } first) return;
            var struck = new List<EnemyTarget> { first };
            DealDamage(c, first, damageMultiplier: firstMultiplier * castMultiplier, extraCritChance: castCrit, onLanded: (cc, dealt) =>
            {
                FirstHitLanded?.Invoke(cc, first, dealt);
                Bounce(cc, struck, castMultiplier, castCrit);
            });
        }, Name);
        ApplyVeteranOfWar(ctx);
        Thrown?.Invoke(ctx);
    }

    /// <summary>
    /// The enemy the first hit strikes: the cast's target in a sim; in a replay the enemy of the log's first hit, which is
    /// often another enemy than the cast's target. Each enemy, the cast's target first, rolls <see cref="FirstHitProcKey"/>
    /// until one is the first hit's.
    /// </summary>
    private EnemyTarget? FirstTarget(SimulationContext ctx, EnemyTarget target)
    {
        if (target is { IsSleeping: false } && ctx.Outcomes.RollProc(FirstHitProcKey, 1.0, ctx, target)) return target;
        var enemies = Enemies(ctx, target);
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != target && ctx.Outcomes.RollProc(FirstHitProcKey, 1.0, ctx, enemies[i])) return enemies[i];
        }
        return null;
    }

    /// <summary>
    /// As a hit lands, the shield bounces to an enemy it has not hit yet, up to <see cref="Constants.MaxNumBounces"/> times. Each bounce keeps
    /// the cast's damage bonuses and crit.
    /// </summary>
    private void Bounce(SimulationContext ctx, List<EnemyTarget> struck, double castMultiplier, double castCrit)
    {
        if (struck.Count > Constants.MaxNumBounces) return;
        var others = ctx.ActiveEnemies.Where(e => !struck.Contains(e)).ToList();
        if (others.Count == 0 || ctx.Outcomes.ChooseTarget(DamageLogId, others, ctx) is not { } next) return;
        if (!ctx.Outcomes.RollProc(BounceProcKey, 1.0, ctx, next)) return;
        struck.Add(next);
        DealDamage(ctx, next, damageMultiplier: castMultiplier, extraCritChance: castCrit, onLanded: (c, _) => Bounce(c, struck, castMultiplier, castCrit));
    }
}
