using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 420 (Mara's Wristbands of Vexira's Prey, item 5225; Hungering Blade Leggings, item 5900): "Your
/// Queen's Fang and Arachnid Assault critical strikes apply Vexira's Venom to enemies, adding 28% of the initial damage
/// accumulatively as poison damage dealt over 6 seconds" (effect 2364, a tick every 2 seconds).
/// Combat logs (pWmcV42K39GHTLQX fight 23, 4,702 ticks): a tick deals a third of the venom, 2s apart shortened by Haste,
/// with a partial tick as it runs out (@ 15.962: a 16,352 crit, then ticks of 1,526); a new crit restarts the 6 seconds
/// and keeps the tick timing. UNVERIFIED: the venom a new crit keeps (taken as a tick's damage per 2s left), and the
/// ticks never crit.
/// </summary>
public class VexirasVenom : Legendary
{
    public const int PowerId = 420;
    public const int WristItemId = 5225;
    public const int LegsItemId = 5900;
    public const int EffectId = 2364;
    public const string EffectName = "Vexira's Venom";
    public const double DamageShare = 0.28;
    public const double VenomSeconds = 6.0;
    public const double TickSeconds = 2.0;
    /// <summary>The venom's damage is dealt in this many ticks' worth (6s / 2s).</summary>
    public const double TicksPerVenom = VenomSeconds / TickSeconds;

    public override int LegendaryPowerId => PowerId;
    public override string Name => EffectName;
    public override string HeroName => "Mara";
    public override IReadOnlyList<int> ItemIds => [WristItemId, LegsItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Mara mara) return;
        var venom = new Venom(mara);
        mara.PoisonDamageIds.Add(venom.DamageLogId);
        mara.Register(venom.Debuff);
        foreach (var finisher in new Ability?[] { mara.GetAbility<QueensFang>(), mara.GetAbility<ArachnidAssault>() })
        {
            if (finisher == null) continue;
            // Mara's own crits: the shadowy clones' add none (@ 20.935 a clone's 6,809 crit leaves the ticks as the
            // Arachnid Assault crit before it made them).
            string own = finisher.Name;
            finisher.Hit += (in hit) =>
            {
                if (hit.IsCrit && hit.Amount > 0 && hit.AbilityName == own && hit.Target is { } target && hit.SimContext is { } ctx)
                {
                    venom.Add(ctx, target, DamageShare * hit.Amount);
                }
            };
        }
    }

    /// <summary>The venom on each enemy: a third of what it holds per tick.</summary>
    private sealed class Venom(Mara owner) : MaraDot(owner, EffectId, EffectName, coefficient: 0.0, TickSeconds, VenomSeconds)
    {
        public void Add(SimulationContext ctx, EnemyTarget target, double damage)
        {
            double kept = IsOn(target, ctx) && Get(target) is { } dot ? dot.Multiplier * Remaining(target, ctx) / TickSeconds : 0.0;
            var applied = Apply(ctx, target);
            applied.Multiplier = (kept + damage) / TicksPerVenom;
        }

        protected override void DealTick(SimulationContext ctx, Instance dot, double fraction) =>
            ctx.ApplyDamage(Owner, dot.Target, dot.Multiplier * fraction, DamageLogId, Name, canCrit: false, 0.0, 1.0);
    }
}
