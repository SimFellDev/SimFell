namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1110 (Spirit ability): "You split your soul into the Sibling of Wind and the Sibling of Earth for 20 seconds. You
/// deal 20% more damage for the duration. SIBLING OF EARTH (controlled): While Twin Souls: Army of One is active, the
/// Sibling of Earth takes 40% reduced damage. SIBLING OF WIND: The Sibling of Wind swiftly teleports to, and strikes, nearby
/// enemies up to 40 times dealing 131.6% of Agility damage with each strike. Can be used during Global Cooldown." Costs 100
/// Spirit Points and grants Spirit of Heroism (+30% Haste). Effect 1547 is the buff; the strikes are
/// <see cref="SiblingOfWind"/>.
/// </summary>
public partial class TwinSoulsArmyOfOne : MeikoAbility
{
    public const int BuffEffectId = 1547;
    public const double BuffSeconds = 20.0;
    /// <summary>Effect 1547: DamageScale x1.2.</summary>
    public const double BuffDamage = 1.2;

    public static readonly AuraDef Buff = AuraDef.Effect("Twin Souls: Army of One", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.DamageScale, BuffDamage));

    public TwinSoulsArmyOfOne(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        IgnoresGcd = true;
        Sibling = new SiblingOfWind(owner);
    }

    /// <summary>The Sibling of Wind's strikes.</summary>
    public SiblingOfWind Sibling { get; }

    /// <summary>Meiko's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
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
        Sibling.Begin(ctx, target);
    }
}
