using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 680: "Your Highwind Arrow grants you 1 stack of Striker's Aim for every Ricochet that does not find its target,
/// granting you +2% Expertise per stack. Every 2 seconds 1 stack of Striker's Aim is removed. Striker's Aim is only
/// reapplied if missing Ricochets exceed the current effect stack count." Effect 3179 (Expertise +0.02, 2s, up to 3
/// stacks). UNVERIFIED: no combat log has this talent.
/// </summary>
public class StrikersAim : Talent
{
    public const int TalentId = 680;
    public const string TalentName = "Striker's Aim";
    public const int BuffEffectId = 3179;
    public const double ExpertisePerStack = 0.02;
    public const double DecaySeconds = 2.0;
    public const int MaxStacks = 3;

    public static readonly AuraDef Buff = AuraDef.Effect("Striker's Aim", BuffEffectId, DecaySeconds, MaxStacks,
        StatModifier.Add(StatType.Expertise, ExpertisePerStack));

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

        // One stack every 2 seconds: the aura runs out at the last stack, and each earlier stack goes as the 2 seconds pass.
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
