using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using ArdeosTalents = FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How fast each Ardeos burn ticks: the burns of Fire Ball, Fire Frogs, Explosivo (Apocalypse's legendary) and Crackling Inferno
/// (Infernal Wave's talent) tick at a fixed rhythm that haste does not change, while Searing Blaze, Engulfing Flames and Incinerate
/// tick faster with haste. Wildfire makes every one of them tick 30% faster.
/// Dev: the time between consecutive ticks of one burn on one enemy is read off the fight's events and compared with the base period,
/// divided by (1 + haste) where haste counts, and by 1.3 in Wildfire. Without Wildfire the first 3 gaps are used (the last tick of a burn
/// can be a partial one). With Wildfire, the burn is started, then Wildfire cast as soon as the hero is free again; only the ticks from
/// that moment to the end of Wildfire's 9 seconds count (a gap that starts before it mixes the two rhythms), at least 2 gaps of them.
/// Incinerate's Spirit of Heroism adds 30% haste, which counts.
/// </summary>
public class ArdeosDotTickSpeed
{
    private const double Tolerance = 0.005;
    private const double WildfireRate = 1.3;
    private const double WildfireSeconds = 9.0;

    /// <summary>One burn: how it is started on a fresh fight and the effect ID and period of its ticks.</summary>
    private sealed record Burn(string Name, int EffectId, double Period, bool HastedTicks, Func<double, StepDriver> Fight, Action<StepDriver> Start, double HasteBonus = 0.0);

    private static readonly Burn[] Burns =
    [
        new("Fire Ball", FireBallDot.EffectId, FireBallDot.PeriodSeconds, false, h => StepFights.Ardeos(h), f => f.Cast<FireBall>()),
        new("Fire Frogs", FireFrogsDot.EffectId, FireFrogsDot.PeriodSeconds, false, h => StepFights.Ardeos(h), f => f.Cast<FireFrogs>()),
        new("Explosivo", Explosivo.DamageEffectId, Explosivo.PeriodSeconds, false,
            h => StepFights.Ardeos(h, legendaries: [Explosivo.HelmItemId]), f => f.Cast<Apocalypse>()),
        new("Crackling Inferno", ArdeosTalents.CracklingInferno.EffectId, ArdeosTalents.CracklingInferno.PeriodSeconds, false,
            h => StepFights.Ardeos(h, talents: [ArdeosTalents.CracklingInferno.TalentId], crit: 1.0), f => f.Cast<InfernalWave>()),
        new("Searing Blaze", SearingBlazeDot.EffectId, SearingBlazeDot.PeriodSeconds, true, h => StepFights.Ardeos(h), f => f.Cast<SearingBlaze>()),
        new("Engulfing Flames", EngulfingFlamesDot.EffectId, EngulfingFlamesDot.PeriodSeconds, true, h => StepFights.Ardeos(h), f => f.Cast<EngulfingFlames>()),
        new("Incinerate", IncinerateDot.EffectId, IncinerateDot.PeriodSeconds, true, h => StepFights.Ardeos(h),
            f => { f.GiveSpiritAbility(); f.Cast<Incinerate>(); }, HasteBonus: 0.3),
    ];

    public static IEnumerable<object[]> Cases(bool hasted) =>
        from burn in Burns
        where burn.HastedTicks == hasted
        from haste in new[] { 0.0, 0.2, 0.4 }
        select new object[] { burn.Name, haste };

    public static IEnumerable<object[]> FixedBurns() => Cases(false);
    public static IEnumerable<object[]> HastedBurns() => Cases(true);

    private static Burn Find(string name) => Burns.Single(b => b.Name == name);

    private static List<double> Times(StepDriver f, Burn burn) => f.Ticks(burn.EffectId).Select(t => t.Time.TotalSeconds).ToList();

    private static List<double> Gaps(List<double> times) => times.Zip(times.Skip(1), (a, b) => b - a).ToList();

    private static void AssertGaps(IEnumerable<double> gaps, double expected, string what)
    {
        var list = gaps.ToList();
        Assert.True(list.Count >= 2, $"{what}: only {list.Count} gaps between ticks");
        Assert.All(list, g => Assert.True(Math.Abs(g - expected) <= Tolerance, $"{what}: expected a tick every {expected:F3}s, got a gap of {g:F3}s"));
    }

    private static void WithoutWildfire(string name, double haste, bool hastedTicks)
    {
        var burn = Find(name);
        var f = burn.Fight(haste);

        burn.Start(f);
        f.Wait(40);

        double rate = hastedTicks ? 1 + haste + burn.HasteBonus : 1.0;
        AssertGaps(Gaps(Times(f, burn)).Take(3), burn.Period / rate, $"{name} at {haste:P0} haste");
    }

    private static void WithWildfire(string name, double haste, bool hastedTicks)
    {
        var burn = Find(name);
        var f = burn.Fight(haste);

        burn.Start(f);
        double from = f.Now.TotalSeconds;
        f.Cast<Wildfire>();
        f.Wait(40);

        double rate = (hastedTicks ? 1 + haste + burn.HasteBonus : 1.0) * WildfireRate;
        var inWildfire = Times(f, burn).Where(t => t >= from && t <= from + WildfireSeconds).ToList();
        AssertGaps(Gaps(inWildfire), burn.Period / rate, $"{name} at {haste:P0} haste in Wildfire");
    }

    /// <summary>
    /// Player: the burns of Fire Ball, Fire Frogs, Explosivo and Crackling Inferno tick at the same rhythm whatever your haste: every 2
    /// seconds (Fire Ball) or 3 seconds (the others).
    /// Dev: one cast of each at 0%, 20% and 40% haste; the first 3 gaps between ticks are the base period.
    /// </summary>
    [Theory]
    [MemberData(nameof(FixedBurns))]
    public void These_Burns_Tick_At_A_Fixed_Rhythm_Whatever_The_Haste(string name, double haste) => WithoutWildfire(name, haste, hastedTicks: false);

    /// <summary>
    /// Player: Wildfire makes the burns of Fire Ball, Fire Frogs, Explosivo and Crackling Inferno tick 30% faster, at any haste.
    /// Dev: one cast of each, then Wildfire; the gaps between ticks while it is up are the base period over 1.3.
    /// </summary>
    [Theory]
    [MemberData(nameof(FixedBurns))]
    public void Wildfire_Makes_These_Burns_Tick_30_Percent_Faster(string name, double haste) => WithWildfire(name, haste, hastedTicks: false);

    /// <summary>
    /// Player: the burns of Searing Blaze (every 2 seconds), Engulfing Flames (1.5) and Incinerate (3) tick faster with haste: the period
    /// over (1 + haste). Incinerate's own Spirit of Heroism adds 30% haste.
    /// Dev: one cast of each at 0%, 20% and 40% haste; the first 3 gaps between ticks are the base period over (1 + haste).
    /// </summary>
    [Theory]
    [MemberData(nameof(HastedBurns))]
    public void These_Burns_Tick_Faster_With_Haste(string name, double haste) => WithoutWildfire(name, haste, hastedTicks: true);

    /// <summary>
    /// Player: Wildfire makes the burns of Searing Blaze, Engulfing Flames and Incinerate tick 30% faster on top of haste.
    /// Dev: one cast of each, then Wildfire; the gaps between ticks while it is up are the base period over (1 + haste) and over 1.3.
    /// </summary>
    [Theory]
    [MemberData(nameof(HastedBurns))]
    public void Wildfire_Makes_These_Burns_Tick_30_Percent_Faster_On_Top_Of_Haste(string name, double haste) => WithWildfire(name, haste, hastedTicks: true);
}
