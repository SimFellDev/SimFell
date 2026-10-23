namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1275: costs 1 Burning Ember (100 Cinders). "Instantly Detonate all active fire damage over time effects from
/// Ardeos abilities on enemies, dealing damage equal to 3.0 seconds of average ticking damage to each enemy."
/// Combat logs (yMAZVJL9hTpFjqbC fight 7): the cast is instant; each enemy with an effect takes 3 hits, about 0.3s apart,
/// each the damage its effects deal per second at the cast (the hits that land later keep it); the hits crit on their
/// own; the effects keep ticking. On many enemies the hits queue up and land up to several seconds late.
/// The hits take the class tuning once more: replays of the two logged builds without Pyrophibian Frenzy
/// (1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma fight 9) come to 1.08 and 1.02 of the logged Detonate damage with it,
/// 1.18 and 1.12 without.
/// </summary>
public class Detonate : ArdeosAbility
{
    public const int AbilityId = 1275;
    public const string AbilityName = "Detonate";
    public const double CinderCost = Ardeos.CindersPerBurningEmber;
    public const double DetonatedSeconds = 3.0;
    public const int HitsPerEnemy = 3;
    public const double FirstHitSeconds = 0.1;
    public const double HitSpacingSeconds = 0.3;

    /// <summary>Buffs that make the next Detonate free, a stack each (Apocalyptic Surge).</summary>
    public List<AuraDef> FreeCastBuffs { get; } = new();

    public Detonate(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power) { }

    private AuraDef? FreeCastBuff(SimTime now) => FreeCastBuffs.FirstOrDefault(b => Owner.Auras.HasAura(b, now));

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
        foreach (var enemy in dots.Targets.ToList())
        {
            double perSecond = 0.0;
            foreach (var dot in dots.On(enemy)) perSecond += dot.Effect.DamagePerSecond(ctx, dot);
            double damage = perSecond * DetonatedSeconds / HitsPerEnemy * CastDamageMultiplier * Owner.ClassDamageTuning;
            if (damage <= 0) continue;
            double critChance = Owner.CritChanceAgainst(enemy, now) + CritChanceBonus + (CurrentCast?.CritChance ?? 0.0);
            double critMultiplier = CritMultiplierAt(now);
            for (int i = 0; i < HitsPerEnemy; i++)
            {
                ctx.Schedule(SimTime.FromSeconds(FirstHitSeconds + i * HitSpacingSeconds), c =>
                {
                    if (!enemy.IsSleeping) c.ApplyDamage(Owner, enemy, damage, DamageLogId, Name, canCrit: true, critChance, critMultiplier);
                }, Name);
            }
        }
    }
}
