using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 447 (Meiko's Twinsoul Leggings, item 5900): "Your Serenity grants you Everlasting Spirit for 9 seconds,
/// reducing all damage you take by 0.40% per stack of Serenity consumed. When Everlasting Spirit expires, you release a
/// pulse of energy onto all enemies around you, dealing 35% Agility damage. An additional 10% Agility damage is added for
/// each stack of Serenity that was consumed." Effect 1510 (9 seconds) is the buff; the pulse is ability 1677, logged as
/// effect 2450, with damage falloff beyond 5 targets. The game data's constants give AgilityCoefficient 0.35 and
/// AgilityCoefficientMultiplierPerStack 0.4, which the tooltip's 10% per stack does not match; the engine takes the
/// tooltip. UNVERIFIED: no combat log has the pulse.
/// </summary>
public class EverlastingSpirit : Legendary
{
    public const int PowerId = 447;
    public const int LeggingsItemId = 5900;
    public const int BuffEffectId = 1510;
    public const double BuffSeconds = 9.0;
    public const int PulseEffectId = 2450;
    public const double PulseCoefficient = 0.35;
    public const double PulseCoefficientPerStack = 0.10;
    public const double TargetThreshold = 5;

    public static readonly AuraDef Buff = AuraDef.Effect("Everlasting Spirit", BuffEffectId, BuffSeconds);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Everlasting Spirit";
    public override string HeroName => "Meiko";
    public override IReadOnlyList<int> ItemIds => [LeggingsItemId];

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
                double coefficient = PulseCoefficient + PulseCoefficientPerStack * stacksConsumed;
                var enemies = c.Outcomes.SelectTargets(pulseId, c.ActiveEnemies, c);
                double falloff = enemies.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / enemies.Count) : 1.0;
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
