namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2296 (Spirit ability) / effect 3242: "Call forth your Bloodbound Spirit, dealing 465.3% of Strength magic
/// damage to all nearby enemies as it emerges and every 1.5 seconds for 6 seconds, applying Rend to the targets for 100%
/// of the damage dealt. For the next 20 seconds your Heart Splitter and Grim Carve deal 25% increased damage." Grants
/// Spirit of Heroism. Damage falls off as sqrt(1/N) beyond 1 enemy (game data). Combat logs (Y4wpb2f3gc8LqJDX fight 25,
/// 5 casts): instant (patch 0.4.5.0, 16 September 2026; the game data still has a 1s cast); 5 pulses, 0.035s after the
/// cast and then every 1.5s, not hasted. Every pulse keeps the damage of the first: with the Strength at each pulse,
/// the engine comes out 2% above the log at 0 stacks of Rising Spirit and 30% above at 3 (fight 25). The first pulse
/// has the +20% Main Stat Drakheim's Absolution grants on the Spirit ability (gpfHLmTBrqcd3Zn4 fight 37,
/// n7zxK8harZT2wjDB fight 9: all 5 pulses 1.15-1.19x the engine's without it).
/// </summary>
public class BloodboundSpirit : GundeAbility
{
    public const int AbilityId = 2296;
    public const string AbilityName = "Bloodbound Spirit";
    public const double Coefficient = 4.653;
    public const double RendShareOfDamage = 1.0;
    public const int Pulses = 5;
    public const double FirstPulseSeconds = 0.035;
    public const double PulseIntervalSeconds = 1.5;
    public const double TargetThreshold = 1;
    public const int BuffEffectId = 3242;
    public const double BuffSeconds = 20.0;
    public const double BuffDamage = 1.25;

    /// <summary>"The damage of your Heart Splitter and Grim Carve is increased by 25%."</summary>
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public BloodboundSpirit(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        RendShare = RendShareOfDamage;
        owner.AbilityDamageMultipliers.Add((ability, now) =>
            ability is HeartSplitter or GrimCarve && owner.Auras.HasAura(Buff, now) ? BuffDamage : 1.0);
    }

    /// <summary>"Serrated Edge does not interact with your Bloodbound Spirit ability" (nor does Crimson Strikes).</summary>
    public override bool TakesRendEmpowerments => false;

    /// <summary>Gunde's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
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
        var hero = Gunde;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        hero.Auras.Apply(Buff, ctx.CurrentTime);
        var rend = BeginRend(ctx);
        // Every pulse keeps the damage, crit chance and crit power of the first, which comes after the buffs the cast
        // grants as it completes (Drakheim's Absolution).
        SimulationContext.HitNumbers? first = null;
        for (int i = 0; i < Pulses; i++)
        {
            ctx.Schedule(SimTime.FromSeconds(FirstPulseSeconds + i * PulseIntervalSeconds), c =>
            {
                var now = c.CurrentTime;
                first ??= new SimulationContext.HitNumbers(BaseDamage(c, null), hero.CritChance(now) + CritChanceBonus, CritMultiplierAt(now));
                Pulse(c, target, rend, first.Value);
            }, Name);
        }
    }

    private void Pulse(SimulationContext ctx, EnemyTarget target, RendCast rend, SimulationContext.HitNumbers cast)
    {
        var targets = AreaTargets(ctx, target);
        if (targets.Count == 0) return;
        double falloff = targets.Count > AoeTargetThreshold ? Math.Sqrt(AoeTargetThreshold / targets.Count) : 1.0;
        var now = ctx.CurrentTime;
        foreach (var enemy in targets)
        {
            var hit = cast with
            {
                BaseDamage = cast.BaseDamage * falloff * Owner.DamageMultiplierAgainst(enemy, now),
                CritChance = cast.CritChance + Owner.CritChanceAgainst(enemy, now) - Owner.CritChance(now),
            };
            ctx.ApplyDamage(Owner, enemy, DamageLogId, Name, canCrit: true, _ => hit, onLanded: (c, dealt) => rend.Landed(c, enemy, dealt), snapshot: true);
        }
    }
}
