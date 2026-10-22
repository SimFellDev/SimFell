using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1023 (Spirit ability) / effect 1387: +20% damage for 20s and 1 Winter Orb every 4s; grants
/// Spirit of Heroism (effect 2253, +30% Haste).
/// </summary>
public class WrathOfWinter : RimeAbility
{
    public const int AbilityId = 1023;
    public const string AbilityName = "Wrath of Winter";
    public const double CastTimeSeconds = 1.5;
    public const int BuffEffectId = 1387;
    public const double BuffSeconds = 20.0;
    public const double BuffDamage = 1.2;
    public const double OrbIntervalSeconds = 4.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Wrath of Winter", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.DamageScale, BuffDamage));

    public WrathOfWinter(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        // Combat logs: cast 0.265s after a Cold Snap, inside its global cooldown (yMAZVJL9hTpFjqbC fight 7 @ 27.284).
        IgnoresGcd = true;
    }

    /// <summary>Rime's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
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
        var hero = Rime;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        hero.Auras.Apply(Buff, ctx.CurrentTime);

        double duration = Buff.Duration.TotalSeconds;
        // Not in the description, but every log shows it: the first orb comes on activation (yMAZVJL9hTpFjqbC fight 7 @ 709.809: 4 -> 5 orbs).
        // The datamine marks the ability scalesWithHaste: the 4 seconds between orbs are hasted (logs: orbs every ~2.4s at ~69% haste),
        // by the haste when the previous orb came (yMAZVJL9hTpFjqbC fight 7: orbs at ~40.2 and ~42.5 despite The Philosopher
        // lapsing 38.44-39.17 in between).
        PeriodicEffect.BeginHasted(ctx, hero, Name, duration, OrbIntervalSeconds, tickOnStart: true, partialFinalTick: false,
            onTick: (c, _) => hero.GenerateWinterOrbs(1, c), retimeOnHasteChange: false);
        TriggerCooldown(ctx);
    }
}
