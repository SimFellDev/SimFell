namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1853 (Spirit ability): "Envelope yourself in solar power, making you Immune to all damage for 3 seconds, ending
/// with an explosion of light that deals magical damage to nearby enemies. Once the immunity fades, you take 50% reduced
/// damage for another 5 seconds. ... Can be used during Global Cooldown." Grants Spirit of Heroism.
/// </summary>
public partial class DecreeOfTheSun : XavianAbility
{
    /// <summary>Game data targetCountFalloff: effect 2561, threshold 8; maxTargets 20.</summary>
    public const double TargetThreshold = 8;

    /// <summary>Effect 2557: immune to all damage for 3 seconds; the explosion is at its end.</summary>
    public static readonly AuraDef Immunity = AuraDef.Effect(AbilityName, 2557, 3.0);

    /// <summary>Effect 2558: the 5 seconds after the immunity.</summary>
    public static readonly AuraDef DamageReduction = AuraDef.Effect("Decree of the Sun: Reduction", 2558, 5.0);

    public DecreeOfTheSun(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.AoeDamageDamageStrengthCoefficient;
        AoeTargetThreshold = TargetThreshold;
        IgnoresGcd = true;
    }

    /// <summary>Xavian's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
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
        var hero = Xavian;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        var immunity = hero.Auras.Apply(Immunity, ctx.CurrentTime);
        ctx.ScheduleAt(immunity.ExpiresAt, c =>
        {
            hero.Auras.Apply(DamageReduction, c.CurrentTime);
            if (c.ActiveEnemies.Count > 0) DealAoeDamage(c, c.ActiveEnemies);
        }, AbilityName);
    }
}
