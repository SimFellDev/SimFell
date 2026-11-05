using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The log's events of an effect the game switches off while another effect is on. The first effect keeps its stack count,
/// and the log shows its switch as `removebuff` and `applybuff`. A spend under Deadly Scheme: Active logs `refreshbuff` with
/// no number when the count falls and `applybuffstack` when it rises, and the `applybuff` at the end has no number when a
/// spend came during Active (asset `GE_Mara_Talent_ResourceAttackSpenderCritChance_Stacker`, `OngoingTagRequirements`
/// IgnoreTags `CustomGrantedTags.Mara.Talent.ResourceAttackSpenderCritChance.Proc`, a tag of effect 2068; combat logs,
/// T2qLPpmMkrAtJ9B1 fight 18 @ 35.812 to 38.916: stacks 40 off, a number-less refresh, 21, a number-less apply, 28).
/// </summary>
public static class InhibitedAuras
{
    /// <summary>The stack of an `applybuff` the log gives no number for.</summary>
    public const int StackNotLogged = -1;

    /// <summary>
    /// Seconds between the effect's switch and the other effect's event, in either order. Combat logs, the 23 Mara fights of
    /// `data/fights`: 0 to 1ms at 6,728 of 6,729 Deadly Scheme switches (T2qLPpmMkrAtJ9B1 fight 18, CwmX2Tb8GA6hyc1v fight 8).
    /// </summary>
    private const double SwitchSeconds = 0.002;

    private static readonly Dictionary<int, int> InhibitorByLogId = new()
    {
        [DeadlyScheme.Stacks.LogId] = DeadlyScheme.Active.LogId,
    };

    /// <summary>
    /// <paramref name="events"/> in time order, with the events of an effect while it is off left out, and its return
    /// holding the last stack logged while it was off. A number-less `refreshbuff` after that stack, or none, leaves
    /// <see cref="StackNotLogged"/>.
    /// </summary>
    public static List<LogAura> Read(List<LogAura> events)
    {
        var read = events;
        foreach (var (effect, inhibitor) in InhibitorByLogId)
        {
            if (read.Any(e => e.AbilityId == inhibitor)) read = ReadEffect(read, effect, inhibitor);
        }
        return read;
    }

    private static List<LogAura> ReadEffect(List<LogAura> events, int effect, int inhibitor)
    {
        var inhibited = InhibitedSpans(events, inhibitor);
        var read = new List<LogAura>(events.Count);
        bool off = false;
        int? held = null;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.AbilityId != effect)
            {
                read.Add(e);
                continue;
            }
            if (!off)
            {
                read.Add(e);
                if (e.Type == "removebuff" && inhibited.Any(s => Overlaps(s, e.Time, NextReturn(events, i, effect))))
                {
                    off = true;
                    held = null;
                }
                continue;
            }
            if (e.Type != "applybuff")
            {
                held = e.Stack;
                continue;
            }
            off = false;
            read.Add(new LogAura
            {
                Time = e.Time,
                Type = e.Type,
                AbilityId = e.AbilityId,
                AbilityName = e.AbilityName,
                Source = e.Source,
                Target = e.Target,
                Stack = e.Stack ?? held ?? StackNotLogged,
                DurationMs = e.DurationMs,
                ExtraAbilityId = e.ExtraAbilityId,
            });
        }
        return read;
    }

    /// <summary>Each span of the inhibiting effect; one with no logged end lasts to the end of the fight.</summary>
    private static List<(double From, double To)> InhibitedSpans(List<LogAura> events, int inhibitor)
    {
        var spans = new List<(double From, double To)>();
        double? on = null;
        foreach (var e in events.Where(e => e.AbilityId == inhibitor))
        {
            if (e.Type == "applybuff")
            {
                on ??= e.Time;
            }
            else if (e.Type == "removebuff" && on is { } from)
            {
                spans.Add((from, e.Time));
                on = null;
            }
        }
        if (on is { } open) spans.Add((open, double.PositiveInfinity));
        return spans;
    }

    private static double NextReturn(List<LogAura> events, int from, int effect)
    {
        for (int i = from + 1; i < events.Count; i++)
        {
            if (events[i].AbilityId == effect && events[i].Type == "applybuff") return events[i].Time;
        }
        return double.PositiveInfinity;
    }

    private static bool Overlaps((double From, double To) span, double from, double to) =>
        span.From <= to + SwitchSeconds && from - SwitchSeconds <= span.To;
}
