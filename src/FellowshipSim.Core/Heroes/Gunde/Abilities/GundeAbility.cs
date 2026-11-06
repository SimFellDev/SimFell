namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

// Each ability lists its game values as constants at the top (IDs and values from the game data).

/// <summary>
/// Base for Gunde's abilities. An ability that "applies Rend to the targets for a share of the damage dealt" sets
/// <see cref="RendShare"/>, starts each cast with <see cref="BeginRend"/> and deals its hits through
/// <see cref="RendingPulse"/>: each hit's damage dealt, times the cast's share, is added to its target's Rend.
/// </summary>
public abstract class GundeAbility : Ability
{
    protected GundeAbility(Gunde owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Gunde Gunde => (Gunde)Owner;

    /// <summary>Share of each hit's damage added to its target's Rend (0: the ability applies no Rend).</summary>
    public double RendShare { get; protected set; }

    /// <summary>
    /// Serrated Edge and Crimson Strikes empower it: every ability that applies Rend but Bloodbound Spirit ("Serrated Edge
    /// does not interact with your Bloodbound Spirit ability").
    /// </summary>
    public virtual bool TakesRendEmpowerments => RendShare > 0.0;

    /// <summary>A pulse of this ability went off (Carnage counts Grim Carve's spins).</summary>
    public event Action<SimulationContext>? Pulsed;

    /// <summary>
    /// The cast's Rend: its share plus Reign in Blood's, and Serrated Edge's, which the cast uses up. Combat logs
    /// (n7zxK8harZT2wjDB fight 9, Chains#3 @ 108.0): Grim Carve uses Serrated Edge as it is cast and every spin gets it
    /// (each spin adds the full Serrated Edge share of its damage).
    /// </summary>
    protected RendCast BeginRend(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        double share = RendShare;
        foreach (var bonus in Gunde.RendShareBonuses) share += bonus(now);
        if (TakesRendEmpowerments && Gunde.Auras.HasAura(BloodArc.SerratedEdge, now))
        {
            share += Gunde.SerratedEdgeShare;
            Gunde.Auras.Remove(BloodArc.SerratedEdge, now);
        }
        double multiplier = 1.0;
        if (TakesRendEmpowerments)
        {
            foreach (var empower in Gunde.RendCastMultipliers) multiplier *= empower(ctx, this);
        }
        return new RendCast(Gunde, share, multiplier);
    }

    /// <summary>
    /// One pulse of hits on <paramref name="targets"/> (losing damage as sqrt(T/N) beyond <see cref="Ability.AoeTargetThreshold"/>):
    /// each hit's damage dealt adds to its target's Rend. <paramref name="onLanded"/> runs after.
    /// </summary>
    protected void RendingPulse(SimulationContext ctx, RendCast rend, IReadOnlyList<EnemyTarget> targets, double damageMultiplier = 1.0,
        Func<EnemyTarget, double>? extraCritChance = null, Action<SimulationContext, EnemyTarget, double>? onLanded = null)
    {
        if (targets.Count == 0) return;
        double multiplier = damageMultiplier * rend.DamageMultiplier;
        if (AoeTargetThreshold > 0 && targets.Count > AoeTargetThreshold) multiplier *= Math.Sqrt(AoeTargetThreshold / targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            DealDamage(ctx, target, damageMultiplier: multiplier, extraCritChance: extraCritChance?.Invoke(target) ?? 0.0,
                onLanded: (c, dealt) =>
                {
                    rend.Landed(c, target, dealt);
                    onLanded?.Invoke(c, target, dealt);
                });
        }
        Pulsed?.Invoke(ctx);
        if (TakesRendEmpowerments) Gunde.RaiseRendPulse(ctx, this);
    }

    /// <summary>The enemies an area pulse around <paramref name="target"/> strikes.</summary>
    protected IReadOnlyList<EnemyTarget> AreaTargets(SimulationContext ctx, EnemyTarget target)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        return ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
    }

    /// <summary>The target alone, while it is in combat.</summary>
    protected static IReadOnlyList<EnemyTarget> Single(EnemyTarget target) => target is { IsSleeping: false } ? [target] : [];
}

/// <summary>
/// The Rend of one cast: the share of its hits' damage that becomes Rend, and the multiplier the cast took on all its
/// hits (Crimson Strikes). Its first landed hit rolls the cast's Spirit Refund (<see cref="Gunde.RendCastLanded"/>).
/// </summary>
public sealed class RendCast(Gunde gunde, double share, double damageMultiplier = 1.0)
{
    private bool _landed;

    public double Share { get; } = share;

    public double DamageMultiplier { get; } = damageMultiplier;

    public void Landed(SimulationContext ctx, EnemyTarget target, double dealt)
    {
        if (dealt <= 0.0) return;
        if (!_landed)
        {
            _landed = true;
            gunde.RendCastLanded(ctx);
        }
        gunde.Rend.Apply(ctx, target, dealt * Share);
    }
}
