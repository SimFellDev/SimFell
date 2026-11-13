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
    /// Volatile Poison rolls its duration between 6 and 8 seconds: over 20 fights each application's roll is inside that range, and the
    /// rolls are not all the same.
    ///
    /// Game data: the duration manager rolls 6 to 8 (NOTES.md, Volatile Poison); combat logs: durations spread evenly from 6.000s to 7.999s.
    /// </summary>
    [Fact]
    public void Volatile_Poison_Rolls_A_Duration_Between_6_And_8_Seconds()
    {
        var rolls = new List<(double Min, double Max, double Seconds)>();
        for (int seed = 1; seed <= 20; seed++)
        {
            var f = StepFights.Mara(seed: seed);
            var durations = f.ForceDurations(_ => null);

            f.Cast<BroodingShadows>(); // into Stealth
            f.Cast<StealthSkitteringBlades>();

            rolls.AddRange(durations.Rolls);
        }

        using var check = new Checks();
        check.Equal(20, rolls.Count, what: "one roll per application");
        check.True(rolls.All(roll => roll.Min == 6 && roll.Max == 8), what: "every roll's range is 6 to 8 seconds");
        check.True(rolls.All(roll => roll.Seconds is >= 6 and <= 8), what: "every duration is from 6 to 8 seconds");
        check.True(rolls.Max(roll => roll.Seconds) - rolls.Min(roll => roll.Seconds) > 0.5, what: "the durations differ");
    }

    /// <summary>
    /// Volatile Poison ticks a sixth of its duration apart, shortened by haste, so the standard tick count applies with that period:
    /// floor(duration / period) full ticks plus a partial one for the time left (at least 0.1 seconds), whatever duration it rolled, then
    /// it erupts once.
    ///
    /// From a Skittering Blades in Stealth, with the duration pinned at the ends of the range and in the middle. Game data: the tick period
    /// scaler is the duration / 6 (NOTES.md, Volatile Poison).
    /// </summary>
    [Theory]
    [InlineData(6.0, 0.0)]
    [InlineData(6.0, 0.13)]
    [InlineData(6.0, 0.3)]
    [InlineData(7.0, 0.0)]
    [InlineData(7.0, 0.13)]
    [InlineData(7.0, 0.3)]
    [InlineData(8.0, 0.0)]
    [InlineData(8.0, 0.13)]
    [InlineData(8.0, 0.3)]
    public void Volatile_Poison_Ticks_Floor_Of_Duration_Over_Period_Plus_A_Partial(double duration, double haste)
    {
        double period = duration / 6 / (1 + haste);
        var f = StepFights.Mara(haste: haste);
        f.ForceDurations(_ => duration);

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<StealthSkitteringBlades>();

        f.Wait(10); // the poison is over and has erupted

        using var check = new Checks(f);
        check.Equal(FullTicks(duration, period) + PartialTick(duration, period, 0.1), f.Ticks(VolatilePoison.EffectId).Count);
        check.Equal(1, f.Ticks(VolatilePoison.EruptionEffectId).Count);
    }
}
