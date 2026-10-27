namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1584 (Spirit ability): "For the next 20 seconds, you deal 20% more damage, gain Cooldown Acceleration equal to
/// your Haste and all Focus costs are reduced by 50%. While Event Horizon is active, your Highwind Arrow reduces the
/// cooldown of Heartseeker Barrage by 0.5 seconds, and every hit from your Heartseeker Barrage reduces the cooldown of
/// Starfall Volley by 1.5 seconds." 0.7s cast; effect 2312 (DamageScale x1.2, 20s); grants Spirit of Heroism.
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
        IgnoresGcd = true;
        owner.FocusCostMultipliers.Add(now => owner.Auras.HasAura(Buff, now) ? CostMultiplier : 1.0);
        owner.CooldownAccelerations.Add((_, now) => owner.Auras.HasAura(Buff, now) ? owner.GetHaste(now) : 0.0);
        owner.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }

    public override void Connect()
    {
        var hero = Elarion;
        var heartseeker = hero.GetAbility<HeartseekerBarrage>();
        var starfall = hero.GetAbility<StarfallVolley>();
        hero.GetAbility<HighwindArrow>()!.Cast += ctx =>
        {
            if (heartseeker != null && hero.Auras.HasAura(Buff, ctx.CurrentTime))
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
