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

    /// <summary>Multiplier on the whole share, bonuses included (Bloodbound Spirit's <c>DotScaler</c>).</summary>
    public double RendShareScaler { get; protected set; } = 1.0;

    /// <summary>
    /// Serrated Edge and Crimson Strikes empower it: every ability that applies Rend but Bloodbound Spirit ("Serrated Edge
    /// does not interact with your Bloodbound Spirit ability").
    /// </summary>
    public virtual bool TakesRendEmpowerments => RendShare > 0.0;

    /// <summary>A pulse of this ability went off (Carnage counts Grim Carve's spins).</summary>
    public event Action<SimulationContext>? Pulsed;

    /// <summary>
    /// The cast's Rend: its share and Serrated Edge's, which the cast uses up. Combat logs (n7zxK8harZT2wjDB fight 9,
    /// Chains#3 @ 108.0): Grim Carve uses Serrated Edge as it is cast and every spin gets it (each spin adds the full
    /// Serrated Edge share of its damage). Reign in Blood's share counts at each hit (<see cref="RendCast.ShareAt"/>).
    /// </summary>
    protected RendCast BeginRend(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        double share = RendShare;
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
        return new RendCast(Gunde, share, RendShareScaler, multiplier);
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
/// <param name="share">The ability's share and Serrated Edge's, fixed at the cast.</param>
/// <param name="scaler">The multiplier on the whole share, Reign in Blood's included (Bloodbound Spirit's <c>DotScaler</c>).</param>
public sealed class RendCast(Gunde gunde, double share, double scaler = 1.0, double damageMultiplier = 1.0)
{
    private bool _landed;

    public double DamageMultiplier { get; } = damageMultiplier;

    /// <summary>
    /// The share of a hit landing now: the cast's share plus Reign in Blood's and Frenzied Reign's as the hit lands, times
    /// the scaler. Blueprint GA_Gunde_Passive_PerTargetAccumulatedBleed_Monitor (build 26320967) tests Reign in Blood's
    /// granted tag on each damage event it handles (offsets 1068 and 2732) and adds the bonuses before the scaler (offsets
    /// 3498, 3573 and 3749). Combat logs (Rend's debuff stacks, 20 fights of patch 0.5.0.0): a Grim Carve spin after Reign
    /// in Blood ends adds 0.49 of its damage from a cast in it (median, 497 spins in 16 fights, 8vb2pTAhFQRcktaD fight 2
    /// and PFtrX9RZJxmv1QfT fight 2 among them), and a spin in it adds 1.05 from a cast before it (219 spins in 13 fights,
    /// 67qwcRKCWYFzyxPA fight 6 and VP9bnawhd6prjfKN fight 8 among them). Bloodbound Spirit's pulses alike: 1.00 out of it
    /// from a cast in it (66 pulses, 9 fights), 2.08 in it from a cast before it (585 pulses, 13 fights, XQHxvNmwRp8TcMJG
    /// fights 2 and 3 among them).
    /// </summary>
    public double ShareAt(SimTime now)
    {
        double total = share;
        foreach (var bonus in gunde.RendShareBonuses) total += bonus(now);
        return total * scaler;
    }

    public void Landed(SimulationContext ctx, EnemyTarget target, double dealt)
    {
        if (dealt <= 0.0) return;
        if (!_landed)
        {
            _landed = true;
            gunde.RendCastLanded(ctx);
        }
        gunde.Rend.Apply(ctx, target, dealt * ShareAt(ctx.CurrentTime));
    }
}
