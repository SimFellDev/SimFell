using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many ticks Mara's periodic effects deal at each haste. Haste speeds the ticks up, so the counts go up in steps (breakpoints). Every
/// test counts the damage events of one application on a single enemy and compares them with the formula.
///
/// A last, partial tick counts only when at least 0.1 seconds are left after the last full one.
/// </summary>
public class MaraHasteBreakpoints
{
    // ---- Hemorrhaging Strike

    /// <summary>
    /// Hemorrhaging Strike's bleed ticks every 3 seconds shortened by haste, for 12 + 3 seconds per Combo Point: floor(duration x (1 +
    /// haste) / 3) full ticks, plus a partial one for the time left (at least 0.1 seconds), whose damage is that fraction of a tick.
    ///
    /// A bleed at 1 Combo Point (15s) and at 2 (18s); the damage of all ticks over a full tick is duration x (1 + haste) / 3, within 0.01.
    /// </summary>
    [Theory]
    [InlineData(1, 0.1)]
    [InlineData(1, 0.2)]
    [InlineData(1, 0.3)]
    [InlineData(2, 0.1)]
    [InlineData(2, 0.25)]
    [InlineData(2, 0.5)]
    public void The_Bleed_Ticks_Floor_Of_Duration_Over_Period_Times_1_Plus_Haste_Plus_A_Partial(int points, double haste)
    {
        double duration = 12 + 3 * points;
        double tick = 3 / (1 + haste);
        var f = StepFights.Mara(haste: haste).WithoutDamageSpread();
        f.Set(Mara.ComboPoints, points);

        f.Cast<HemorrhagingStrike>();
        f.Wait(duration + 5); // the bleed is over
        var ticks = f.Damage("Hemorrhage");

        using var check = new Checks(f);
        check.Equal(FullTicks(duration, tick) + PartialTick(duration, tick, 0.1), ticks.Count);
        check.Equal(duration * (1 + haste) / 3, ticks.Sum(t => t.Value) / ticks[0].Value, 0.01);
    }

    // ---- Seething Poison

    /// <summary>
    /// Seething Poison ticks every 2 seconds shortened by haste, for 60 seconds that haste does not shorten: floor(60 x (1 + haste) / 2) full
    /// ticks plus a partial one.
    ///
    /// From a Widow's Bite in Stealth, at hastes where the 60 seconds are not a whole number of ticks.
    /// </summary>
    [Theory]
    [InlineData(0.05)]
    [InlineData(0.15)]
    [InlineData(0.25)]
    [InlineData(0.35)]
    public void Seething_Poison_Ticks_Floor_Of_60_Over_Period_Times_1_Plus_Haste_Plus_A_Partial(double haste)
    {
        double tick = 2 / (1 + haste);
        var f = StepFights.Mara(haste: haste);

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<WidowsBite>();

        f.Wait(65); // the poison is over

        Assert.Equal(FullTicks(60, tick) + PartialTick(60, tick, 0.1), f.Damage("Seething Poison").Count);
    }

    // ---- Volatile Poison

    /// <summary>
    /// Volatile Poison rolls a duration of 6 to 8 seconds and ticks every sixth of it, shortened by haste: floor(6 x (1 + haste)) full ticks
    /// plus a partial one for the time left (at least 0.1 seconds), then it erupts once.
    ///
    /// From a Skittering Blades in Stealth. The harness cannot force the duration roll, so the hastes are ones where the count is the same at
    /// 6 and at 8 seconds, and the test checks the formula at both ends. Game data: the duration manager rolls 6 to 8, the tick period
    /// scaler is the duration / 6 (NOTES.md, Volatile Poison).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.13)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    [InlineData(0.43)]
    [InlineData(0.7)]
    public void Volatile_Poison_Ticks_6_Times_1_Plus_Haste_Plus_A_Partial(double haste)
    {
        int TicksIn(double duration) =>
            FullTicks(duration, duration / 6 / (1 + haste)) + PartialTick(duration, duration / 6 / (1 + haste), 0.1);
        var f = StepFights.Mara(haste: haste);

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<StealthSkitteringBlades>();

        f.Wait(10); // the poison is over and has erupted

        using var check = new Checks(f);
        check.Equal(TicksIn(6), TicksIn(8)); // the roll does not change the count
        check.Equal(TicksIn(6), f.Ticks(VolatilePoison.EffectId).Count);
        check.Equal(1, f.Ticks(VolatilePoison.EruptionEffectId).Count);
    }
}
