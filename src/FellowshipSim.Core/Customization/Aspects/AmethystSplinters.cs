using System.Globalization;
using System.Text.RegularExpressions;
using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 31: "When you deal Critical Damage to an enemy, you deal an additional X% of that damage to them over 8 seconds"
/// (effect 101, cannot crit). One Amethyst Splinter per target. Like a hasted damage-over-time effect, each tick is
/// worth a quarter of its damage (8 seconds of 2-second ticks) and haste adds ticks within the 8 seconds, so it deals
/// X% x (1 + haste) in total. A crit adds its share to what the time left would still deal and restarts the 8 seconds
/// on the same effect; ticks keep their rhythm (`GA_Trait_GemDotHotOnCrit` offsets 493 to 1428, new value = (crit x share
/// + left x value / 2) / 4; RefreshActiveEffectDuration 0x145241E40 resets the duration timer alone, and the next tick
/// reads the new magnitude, 0x144A911F0, build 26320967). The period is divided by the instigator's Haste at application
/// (0x144A6B413 to 0x1452D5C70), and each Haste change of the hero re-arms a running splinter's period with the fraction left
/// carried over, no tick and no partial tick (0x1452F2830, 0x1452F2A70, 0x1452DC7E0), as `HastedTimer.Update` does. When the 8
/// seconds run out the splinter ends with a partial tick (CheckDuration 0x144A6FAB0), its
/// factor elapsed / period, skipped under MinimumPartialTickTime 0.1 (`DefaultGame.ini`, 0x1452D4B70), and the next crit
/// applies a new effect with a new period timer (`GA_Trait_GemDotHotOnCrit` offsets 1861 to 2201). Combat logs, 23 Mara
/// fights: 569 of 799 removals not on a death have a tick in their frame, T2qLPpmMkrAtJ9B1 fight 18 @ 53.144: a tick of
/// 673 after ticks of 985, 8.013s after the crit. It ends on the enemy's death and on the hero's
/// (EffectLogic.RemoveOnDeath, EffectLogic.RemoveOnSourceDeath), with no partial tick.
/// </summary>
public class AmethystSplinters : Aspect
{
    public const int AspectId = 31;
    public const string AspectName = "Amethyst Splinters";
    public const int DotEffectId = 101;
    public const string DotName = "Amethyst Splinter";
    /// <summary>TraitConstants Trait.GemDotHotOnCrit.Debuff.Duration, every rank.</summary>
    public const double DurationSeconds = 8.0;
    /// <summary>TraitConstants Trait.GemDotHotOnCrit.Debuff.TickRate, every rank; hasted, no tick on application.</summary>
    public const double TickPeriodSeconds = 2.0;
    public const double BaseTicks = DurationSeconds / TickPeriodSeconds;
    /// <summary>Share of each critical hit's damage dealt again over 8 seconds (TraitConstants Trait.GemDotHotOnCrit.Debuff.DamagePercentage).</summary>
    public static readonly double[] DamageShareByRank = [0.04, 0.06, 0.08, 0.11];

    public override int Id => AspectId;
    public override string Name => AspectName;

    /// <summary>
    /// One application on one enemy, from the crit that finds no splinter running until its 8 seconds run out. Effect 101 is
    /// tagged EffectType.Dot: its ticks run on the game's period timer, with a partial tick (the hasted progress left) on the
    /// server frame after the 8 seconds run out.
    /// </summary>
    private sealed class Splinter
    {
        public double TickValue;
        public SimTime EndsAt;
        public PeriodicEffect? Effect;

        public void Begin(SimulationContext ctx, Hero hero, EnemyTarget target, int logId)
        {
            Effect = PeriodicEffect.BeginHasted(ctx, hero, DotName, DurationSeconds, TickPeriodSeconds, tickOnStart: false, partialFinalTick: true,
                (c, fraction) =>
                {
                    if (target.IsSleeping)
                    {
                        Effect?.Stop(c);
                        return;
                    }
                    // The crit's damage already includes the hero's damage buffs, so the ticks are not scaled again.
                    c.ApplyDamage(hero, target, TickValue * fraction, logId, DotName, canCrit: false, 0.0, 1.0);
                },
                damageLogId: logId, target: target, damageOverTime: true);
        }
    }

    public override void Bind(Hero hero)
    {
        double share = ForRank(DamageShareByRank);
        int logId = LogIds.Effect(DotEffectId);
        hero.DotDamageIds.Add(logId);
        var splinters = new Dictionary<EnemyTarget, Splinter>();
        // The fight the splinters belong to (each sim iteration and replay has its own).
        SimulationContext? fight = null;

        hero.OnDied += ctx =>
        {
            if (fight != ctx) return;
            foreach (var splinter in splinters.Values) splinter.Effect?.Stop(ctx);
        };

        hero.OnDamageDealt += (in dmg) =>
        {
            if (!dmg.IsCrit || dmg.Target == null || dmg.Target.IsSleeping || dmg.SimContext == null || dmg.AbilityId == logId) return;
            var ctx = dmg.SimContext;
            var now = ctx.CurrentTime;
            if (fight != ctx)
            {
                fight = ctx;
                splinters.Clear();
                ctx.EnemyDemising += (c, enemy) =>
                {
                    if (splinters.TryGetValue(enemy, out var dying)) dying.Effect?.Stop(c);
                };
            }
            // A crit with the splinter running keeps the rhythm and restarts the 8 seconds; the damage not yet dealt (the time
            // left in unhasted ticks) carries over into them. A crit after the 8 seconds ran out starts a new rhythm.
            if (splinters.TryGetValue(dmg.Target, out var s) && s.EndsAt > now && s.Effect is { IsActive: true } running)
            {
                double left = s.TickValue * (s.EndsAt - now).TotalSeconds / TickPeriodSeconds;
                s.TickValue = (left + dmg.Amount * share) / BaseTicks;
                s.EndsAt = now + SimTime.FromSeconds(DurationSeconds);
                running.RestartDuration(ctx, DurationSeconds);
                return;
            }
            splinters[dmg.Target] = s = new Splinter { TickValue = dmg.Amount * share / BaseTicks, EndsAt = now + SimTime.FromSeconds(DurationSeconds) };
            s.Begin(ctx, hero, dmg.Target, logId);
        };
    }
}
