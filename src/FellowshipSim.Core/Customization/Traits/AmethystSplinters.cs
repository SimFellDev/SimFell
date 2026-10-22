using System.Globalization;
using System.Text.RegularExpressions;
using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 31: "When you deal Critical Damage to an enemy, you deal an additional X% of that damage to them over 8 seconds"
/// (effect 101, cannot crit). One Amethyst Splinter per target. Like a hasted damage-over-time effect, each tick is
/// worth a quarter of its damage (8 seconds of 2-second ticks) and haste adds ticks within the 8 seconds, so it deals
/// X% x (1 + haste) in total. A crit adds its share to what the ticks left would still deal and restarts the 8 seconds;
/// ticks keep their rhythm. (Combat logs, 10 Rime logs: splinter damage is ~1.5x X% of crit damage at every rank; the
/// datamine gives no tick period.)
/// </summary>
public class AmethystSplinters : Trait
{
    public const int TraitId = 31;
    public const string TraitName = "Amethyst Splinters";
    public const int DotEffectId = 101;
    public const string DotName = "Amethyst Splinter";
    public const double DurationSeconds = 8.0;
    public const double TickPeriodSeconds = 2.0;
    public const double BaseTicks = DurationSeconds / TickPeriodSeconds;
    /// <summary>Share of each critical hit's damage dealt again over 8 seconds.</summary>
    public static readonly double[] DamageShareByRank = [0.04, 0.06, 0.08, 0.11];

    public override int Id => TraitId;
    public override string Name => TraitName;

    /// <summary>The splinter on one enemy. It keeps its own tick handler, so a tick allocates none.</summary>
    private sealed class Splinter
    {
        public Splinter(Hero hero, EnemyTarget target, int logId)
        {
            Tick = ctx =>
            {
                NextTick = null;
                if (target.IsSleeping || ctx.CurrentTime >= EndsAt) return;
                // The crit's damage already includes the hero's damage buffs, so the ticks are not scaled again.
                ctx.ApplyDamage(hero, target, TickValue, logId, DotName, canCrit: false, 0.0, 1.0);
                // Keep the rhythm: a crit landing before the next tick restarts the 8 seconds.
                NextTick = HastedTimer.Start(ctx, hero, TickPeriodSeconds, Tick!, DotName);
            };
        }

        public double TickValue;
        public SimTime EndsAt;
        public HastedTimer? NextTick;
        public readonly Action<SimulationContext> Tick;
    }

    public override void Bind(Hero hero)
    {
        double share = ForRank(DamageShareByRank);
        int logId = LogIds.Effect(DotEffectId);
        var splinters = new Dictionary<EnemyTarget, Splinter>();
        // The fight the splinters belong to (each sim iteration and replay has its own).
        SimulationContext? fight = null;

        hero.OnDamageDealt += dmg =>
        {
            if (!dmg.IsCrit || dmg.Target == null || dmg.Target.IsSleeping || dmg.SimContext == null || dmg.AbilityId == logId) return;
            var ctx = dmg.SimContext;
            var now = ctx.CurrentTime;
            if (fight != ctx)
            {
                fight = ctx;
                splinters.Clear();
            }
            if (!splinters.TryGetValue(dmg.Target, out var s)) splinters[dmg.Target] = s = new Splinter(hero, dmg.Target, logId);
            // The damage not yet dealt (the time left in unhasted ticks) carries over into the new 8 seconds.
            double left = s.EndsAt > now ? s.TickValue * (s.EndsAt - now).TotalSeconds / TickPeriodSeconds : 0.0;
            s.TickValue = (left + dmg.Amount * share) / BaseTicks;
            s.EndsAt = now + SimTime.FromSeconds(DurationSeconds);
            s.NextTick ??= HastedTimer.Start(ctx, hero, TickPeriodSeconds, s.Tick, DotName);
        };
    }
}
