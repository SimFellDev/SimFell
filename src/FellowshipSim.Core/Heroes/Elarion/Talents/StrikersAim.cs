using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 680: Highwind Arrow grants a stack of Striker's Aim for every Ricochet that does not find its target,
/// granting Expertise per stack. Periodically a stack of Striker's Aim is removed. Striker's Aim is only
/// reapplied if missing Ricochets exceed the current effect stack count. Effect 3179 (Expertise, a decay period, capped
/// stacks). UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class StrikersAim : Talent
{
    public const int BuffEffectId = 3179;

    public static readonly AuraDef Buff = AuraDef.Effect("Striker's Aim", BuffEffectId, BuffDuration, MaxStacks,
        StatModifier.Add(StatType.Expertise, ExpertiseAdded));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        highwind.RicochetsMissed += (ctx, missed) =>
        {
            int stacks = Math.Min(MaxStacks, missed);
            if (stacks <= hero.Auras.GetStacks(Buff, ctx.CurrentTime)) return;
            hero.Auras.Apply(Buff, ctx.CurrentTime, setStacks: stacks);
            Decay(ctx, hero.Auras.Get(Buff, ctx.CurrentTime)!);
        };

        // One stack every `BuffDuration`: the aura runs out at the last stack, and each earlier stack goes as the period passes.
        void Decay(SimulationContext ctx, Aura aura)
        {
            var at = aura.ExpiresAt;
            ctx.ScheduleAt(at - SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds), c =>
            {
                var held = hero.Auras.Get(Buff, c.CurrentTime);
                if (held != aura || held.ExpiresAt != at || held.Stacks <= 1) return;
                hero.Auras.Apply(Buff, c.CurrentTime, setStacks: held.Stacks - 1);
                Decay(c, held);
            }, TalentName);
        }
    }
}
