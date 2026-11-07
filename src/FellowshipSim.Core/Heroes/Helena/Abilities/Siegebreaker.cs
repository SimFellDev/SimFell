namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 1515 (Spirit ability): "For the next 20 seconds, the cooldown reduction granted by your Veteran of War is
/// doubled. While Siegebreaker is active, you take 25% reduced damage and your Shield Slam, Shield Throw and Shockwave
/// abilities deal 25% increased damage." Grants Spirit of Heroism. The buff is effect 2192.
/// </summary>
public class Siegebreaker : HelenaAbility
{
    public const int AbilityId = 1515;
    public const string AbilityName = "Siegebreaker";
    public const int BuffEffectId = 2192;
    public const double BuffSeconds = 20.0;
    public const double AbilityDamage = 1.25;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public Siegebreaker(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        IgnoresGcd = true;
        owner.AbilityDamageMultipliers.Add((ability, now) =>
            ability is ShieldSlam or ShieldThrow or Shockwave && owner.Auras.HasAura(Buff, now) ? AbilityDamage : 1.0);
    }

    /// <summary>Helena's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
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
        var hero = Helena;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
