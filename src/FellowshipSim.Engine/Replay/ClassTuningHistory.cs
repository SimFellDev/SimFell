using FellowshipSim.Core.Heroes.Rime;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Past class tuning values. The engine has each hero's current value; a replay of a fight from before a change gives the
/// hero the value in force on the fight's date, so older logs still compare like for like.
/// </summary>
public static class ClassTuningHistory
{
    /// <summary>On <paramref name="From"/>, <paramref name="HeroType"/>'s class tuning changed; before then it was <paramref name="Before"/>.</summary>
    public sealed record Change(Type HeroType, DateTime From, double Before);

    public static readonly Change[] All =
    [
        // Rime: BalancingDamageAura raised its damage on 2026-08-27 (per the user; logs before then run below it).
        new(typeof(Rime), new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc), Before: 1.0),
    ];

    /// <summary>Gives <paramref name="hero"/> the class tuning in force on <paramref name="fightDate"/>.</summary>
    public static void Apply(Hero hero, DateTime? fightDate)
    {
        if (fightDate == null) return;
        // The earliest change after the fight holds the value the fight had.
        var change = All.Where(c => c.HeroType == hero.GetType() && fightDate < c.From).MinBy(c => c.From);
        if (change != null) hero.ClassDamageTuning = change.Before;
    }
}
