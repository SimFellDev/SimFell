namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1275: costs a Burning Ember (<see cref="CinderCost"/>). Instantly detonates all active fire damage over time
/// effects from Ardeos abilities on enemies, dealing damage equal to <see cref="Constants.DotDamageSampleDuration"/> of average ticking
/// damage to each enemy.
/// Combat logs (yMAZVJL9hTpFjqbC fight 7): the cast is instant; each enemy with an effect takes <see cref="Constants.NumHitsPerTarget"/>
/// hits, spaced by <see cref="Constants.BetweenHitDelay"/>, each the damage its effects deal per second at the cast (the hits that
/// land later keep it); the hits crit on their own; the effects keep ticking. On many enemies the hits queue up and land
/// late.
/// </summary>
public partial class Detonate : ArdeosAbility
{
    public const double CinderCost = Ardeos.CindersPerBurningEmber;
    public const double FirstHitSeconds = 0.1;

    /// <summary>Buffs that make the next Detonate free, a stack each (Apocalyptic Surge).</summary>
    public List<AuraDef> FreeCastBuffs { get; } = new();

    /// <summary>
    /// Detonate's global cooldown, not hasted: in server frames, longer without a legendary item than with one (combat logs;
    /// see <see cref="UseLegendaryGlobalCooldown"/>). The other instants take the hero's global cooldown, hasted.
    /// </summary>
    public const double GlobalCooldown = 31.0 / 30.0;

    /// <summary>The global cooldown with a legendary item worn, in server frames.</summary>
    public const double GlobalCooldownWithLegendary = 29.0 / 30.0;

    public Detonate(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        GlobalCooldownSeconds = GlobalCooldown;
        GlobalCooldownScalesWithHaste = AbilityGlobalCooldownScalesWithHaste;
    }

    // Speed: the enemies with an effect at the cast, in a list kept between casts.
    private readonly List<EnemyTarget> _targets = new();

    private AuraDef? FreeCastBuff(SimTime now)
    {
        foreach (var buff in FreeCastBuffs)
        {
            if (Owner.Auras.HasAura(buff, now)) return buff;
        }
        return null;
    }

    /// <summary>
    /// A legendary item's cooldown recovery (Strand of Eternity) shortens Detonate's global cooldown from <see cref="GlobalCooldown"/> to
    /// <see cref="GlobalCooldownWithLegendary"/>. Called once when the hero is built (<see cref="Hero.FinalizeBuild"/>).
    /// </summary>
    public void ModifyGlobalCooldownFromStrandOfEternity() => GlobalCooldownSeconds = GlobalCooldownWithLegendary;

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (FreeCastBuff(ctx.CurrentTime) == null && Owner.ResourceAmount(Ardeos.Cinders) < CinderCost - 1e-9)
        {
            reason = explain ? $"{Name} needs a Burning Ember ({Owner.ResourceAmount(Ardeos.Cinders):0.#} Cinders)" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        if (FreeCastBuff(now) is { } free) Owner.Auras.ConsumeStack(free, now);
        else Owner.SpendResource(Ardeos.Cinders, CinderCost, ctx);

        var dots = Ardeos.Dots;
        dots.TargetsInto(_targets);
        foreach (var enemy in _targets)
        {
            double perSecond = 0.0;
            var on = dots.On(enemy);
            for (int i = 0; i < on.Count; i++) perSecond += on[i].Effect.DamagePerSecond(ctx, on[i]);
            double damage = perSecond * Constants.DotDamageSampleDuration / Constants.NumHitsPerTarget * CastDamageMultiplier;
            if (damage <= 0) continue;
            double critChance = Owner.CritChanceAgainst(enemy, now) + CritChanceBonus + (CurrentCast?.CritChance ?? 0.0);
            double critMultiplier = CritMultiplierAt(now);
            for (int i = 0; i < Constants.NumHitsPerTarget; i++)
            {
                ctx.Schedule(SimTime.FromSeconds(FirstHitSeconds + i * Constants.BetweenHitDelay), c =>
                {
                    if (!enemy.IsSleeping) c.ApplyDamage(Owner, enemy, damage, DamageLogId, Name, canCrit: true, critChance, critMultiplier);
                }, Name);
            }
        }
    }
}
