namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1584 (Spirit ability): while active, the hero deals more damage, gains Cooldown Acceleration equal to
/// Haste and has reduced Focus costs. Highwind Arrow reduces the cooldown of Heartseeker Barrage, and every hit from
/// Heartseeker Barrage reduces the cooldown of Starfall Volley. Has a cast time; effect 2312 (DamageScale); grants Spirit of Heroism.
/// </summary>
public class EventHorizon : ElarionAbility
{
    public const int AbilityId = 1584;
    public const string AbilityName = "Event Horizon";
    public const double CastTimeSeconds = 0.7;
    public const int BuffEffectId = 2312;
    public const double BuffSeconds = 20.0;
    public const double BuffDamage = 1.2;
    public const double CostMultiplier = 0.5;
    public const double HeartseekerReductionSeconds = 0.5;
    public const double StarfallReductionSeconds = 1.5;

    public static readonly AuraDef Buff = AuraDef.Effect("Event Horizon", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.DamageScale, BuffDamage));

    public EventHorizon(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        // On the global cooldown, which lasts as long as its cast (and so cannot start inside another cast).
        GlobalCooldownSeconds = CastTimeSeconds;
        GlobalCooldownScalesWithHaste = true;
        owner.FocusCostMultipliers.Add(now => owner.Auras.HasAura(Buff, now) ? CostMultiplier : 1.0);
        // Effect 2314: MultiplyCompound on CooldownRecovery, by the Haste attribute.
        owner.CooldownRecoveryCompounds.Add(now => owner.Auras.HasAura(Buff, now) ? 1.0 + owner.GetHaste(now) : 1.0);
        owner.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }

    public override void Connect()
    {
        var hero = Elarion;
        var heartseeker = hero.GetAbility<HeartseekerBarrage>();
        var starfall = hero.GetAbility<StarfallVolley>();
        // Every Highwind Arrow damage event counts, with
        // no bounce or per-cast test. Each ricochet raises one, and a crit raises one.
        hero.GetAbility<HighwindArrow>()!.Hit += (in hit) =>
        {
            if (heartseeker != null && hit.SimContext is { } ctx && hero.Auras.HasAura(Buff, ctx.CurrentTime))
                hero.ReduceCooldown(heartseeker.Name, SimTime.FromSeconds(HeartseekerReductionSeconds), ctx.CurrentTime);
        };
        if (heartseeker == null || starfall == null) return;
        heartseeker.ArrowLanded += ctx =>
        {
            if (hero.Auras.HasAura(Buff, ctx.CurrentTime))
                hero.ReduceCooldown(starfall.Name, SimTime.FromSeconds(StarfallReductionSeconds), ctx.CurrentTime);
        };
    }

    /// <summary>The Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        double points = Owner.ResourceAmount(Hero.SpiritPoints);
        if (points < Owner.SpiritAbilityCost)
        {
            reason = explain ? $"{Name} needs {Owner.SpiritAbilityCost:0} Spirit Points (have {points:0.#})" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Owner;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
