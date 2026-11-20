using System.Globalization;
using System.Text.RegularExpressions;
using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 31: "When you deal Critical Damage to an enemy, you deal an additional X% of that damage to them over a duration"
/// (effect 101, cannot crit). One Amethyst Splinter per target. Like a hasted damage-over-time effect, each tick is
/// an equal share of its damage (<see cref="DurationSeconds"/> divided by <see cref="TickPeriodSeconds"/> ticks) and haste adds ticks within
/// the duration, so it deals X% x (1 + haste) in total. A crit adds its share to what the time left would still deal and
/// restarts the duration on the same effect; ticks keep their rhythm. The new tick value is
/// the crit's share plus the time left's unhasted ticks, divided by <see cref="BaseTicks"/>. A restart resets the duration
/// timer alone, and the next tick reads the new magnitude. The period is divided by the instigator's Haste at application,
/// and each Haste change of the hero re-arms a running splinter's period with the fraction left carried over, no tick and
/// no partial tick, as `HastedTimer.Update` does. When the duration runs out the splinter ends with a partial tick, its
/// factor elapsed / period, skipped under the game's minimum partial tick time (`DefaultGame.ini`), and the next crit
/// applies a new effect with a new period timer. Combat logs, 23 Mara
/// fights: most removals not on a death have a tick in their frame, T2qLPpmMkrAtJ9B1 fight 18 @ 53.144: a tick after
/// the full ticks, one duration after the crit. It ends on the enemy's death and on the hero's
/// (EffectLogic.RemoveOnDeath, EffectLogic.RemoveOnSourceDeath), with no partial tick.
/// </summary>
public partial class AmethystSplinters : Aspect
{
    public const int DotEffectId = 101;
    public const string DotName = "Amethyst Splinter";
    /// <summary>Game data, every rank.</summary>
    public const double DurationSeconds = 8.0;
    /// <summary>Game data, every rank; hasted, no tick on application.</summary>
    public const double TickPeriodSeconds = 2.0;
    public const double BaseTicks = DurationSeconds / TickPeriodSeconds;

    public override int Id => AspectId;
    public override string Name => AspectName;

    /// <summary>
    /// One application on one enemy, from the crit that finds no splinter running until its duration runs out. Effect 101 is
    /// tagged EffectType.Dot: its ticks run on the game's period timer, with a partial tick (the hasted progress left) on the
    /// server frame after the duration runs out.
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
        double share = ForRank(Constants.DebuffDamagePercentageByRank);
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
            // A crit with the splinter running keeps the rhythm and restarts the duration; the damage not yet dealt (the time
            // left in unhasted ticks) carries over into them. A crit after the duration ran out starts a new rhythm.
            if (splinters.TryGetValue(dmg.Target, out var s) && s.EndsAt > now && s.Effect is { IsActive: true } running)
            {
                double left = s.TickValue * (s.EndsAt - now).TotalSeconds / TickPeriodSeconds;
                s.TickValue = (left + dmg.Amount * share) / BaseTicks;
                s.EndsAt = now + SimTime.FromSeconds(DurationSeconds);
                running.Refresh(ctx, DurationSeconds);
                return;
            }
            splinters[dmg.Target] = s = new Splinter { TickValue = dmg.Amount * share / BaseTicks, EndsAt = now + SimTime.FromSeconds(DurationSeconds) };
            s.Begin(ctx, hero, dmg.Target, logId);
        };
    }
}
