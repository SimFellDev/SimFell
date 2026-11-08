using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 447: "Your Serenity grants you Everlasting Spirit for 9 seconds, reducing all damage you take by 0.40%
/// per stack of Serenity consumed. When Everlasting Spirit expires, you release a pulse of energy onto all enemies around
/// you, dealing 35% Agility damage. An additional 10% Agility damage is added for each stack of Serenity that was
/// consumed." Effect 1510 is the buff; the pulse is ability 1677, logged as effect 2450, with damage falloff beyond
/// <see cref="TargetCountDamageScalingThreshold"/> targets. The game data's <see cref="AgilityCoefficientMultiplierPerStack"/>
/// differs from the tooltip's 10% per stack; the engine takes the tooltip. UNVERIFIED: no combat log has the pulse.
/// </summary>
public partial class EverlastingSpirit : Legendary
{
    public const int BuffEffectId = 1510;
    public const int PulseEffectId = 2450;
    /// <summary>Tooltip: "An additional 10% Agility damage is added for each stack of Serenity that was consumed."</summary>
    public const double PulseCoefficientPerStack = 0.10;

    public static readonly AuraDef Buff = AuraDef.Effect("Everlasting Spirit", BuffEffectId, Duration);

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<Serenity>() is not { } serenity) return;
        int pulseId = LogIds.Effect(PulseEffectId);
        int stacksConsumed = 0;
        serenity.Soothed += (ctx, stacks) =>
        {
            stacksConsumed = stacks;
            var applied = hero.Auras.Apply(Buff, ctx.CurrentTime);
            var expiry = applied.ExpiresAt;
            ctx.ScheduleAt(expiry, c =>
            {
                // A later Serenity refreshed the buff: its own expiry pulses.
                if (hero.Auras.Get(Buff, c.CurrentTime) is { } aura && aura.ExpiresAt > expiry) return;
                double coefficient = AgilityCoefficient + PulseCoefficientPerStack * stacksConsumed;
                var enemies = c.Outcomes.SelectTargets(pulseId, c.ActiveEnemies, c);
                double threshold = TargetCountDamageScalingThreshold;
                double falloff = enemies.Count > threshold ? Math.Sqrt(threshold / enemies.Count) : 1.0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    c.ApplyDamage(hero, enemy, pulseId, Name, canCrit: true, x => new SimulationContext.HitNumbers(
                        coefficient * falloff * hero.PrimaryAttribute(x.CurrentTime) * (1.0 + hero.Expertise(x.CurrentTime)) * hero.DamageScale(x.CurrentTime),
                        hero.CritChanceAgainst(enemy, x.CurrentTime), hero.CritMultiplier(x.CurrentTime)));
                }
            }, Name);
        };
    }
}
