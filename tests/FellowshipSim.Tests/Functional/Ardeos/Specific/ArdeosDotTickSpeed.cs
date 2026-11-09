using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How fast each Ardeos damage over time effect ticks. The burns of Fire Ball, Fire Frogs, Explosivo (Apocalypse's legendary) and Crackling
/// Inferno (Infernal Wave's talent) tick at a fixed rhythm that haste does not change; Searing Blaze, Engulfing Flames and Incinerate's
/// burn tick faster with haste. Wildfire makes every one of them tick 30% faster.
///
/// The rhythm is the time between consecutive ticks of one effect: the first 3 gaps without Wildfire (the last tick can be partial), and
/// with Wildfire the gaps within its 9 seconds. How many ticks each effect deals is in ArdeosHasteBreakpoints.cs.
/// </summary>
public class ArdeosDotTickSpeed
{
    /// <summary>How far a gap may be from the expected rhythm: 5ms.</summary>
    private const double GapTolerance = 0.005;

    /// <summary>Prepare a bare Ardeos ready to apply <paramref name="ability"/>.</summary>
    private static StepDriver ArdeosFor(string ability, double haste)
    {
        var f = ability switch
        {
            "Apocalypse" => StepFights.Ardeos(haste: haste, legendaries: [Explosivo.HelmItemId]),
            // Crackling Inferno's burn comes from a crit
            "Infernal Wave" => StepFights.Ardeos(haste: haste, talents: [CracklingInferno.TalentId], crit: 1.0),
            _ => StepFights.Ardeos(haste: haste),
        };
        if (ability == "Incinerate") f.Set(Hero.SpiritPoints, 100); // enough to cast it
        return f;
    }

    /// <summary>The times between consecutive ticks, in seconds.</summary>
    private static List<double> GapsBetween(IEnumerable<StepEvent> ticks)
    {
        var times = ticks.Select(tick => tick.Time.TotalSeconds).ToList();
        return times.Zip(times.Skip(1), (earlier, later) => later - earlier).ToList();
    }

    // ---- Fixed rhythm: Fire Ball, Fire Frogs, Explosivo and Crackling Inferno

    /// <summary>
    /// The burns of Fire Ball, Fire Frogs, Explosivo and Crackling Inferno tick every 2 seconds (Fire Ball) or 3 (the others), whatever
    /// your haste.
    /// </summary>
    [Theory]
    [InlineData("Fire Ball", FireBallDot.EffectId, 2.0, 0.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 2.0, 0.2)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 2.0, 0.4)]
    [InlineData("Fire Frogs", FireFrogsDot.EffectId, 3.0, 0.0)]
    [InlineData("Fire Frogs", FireFrogsDot.EffectId, 3.0, 0.2)]
    [InlineData("Fire Frogs", FireFrogsDot.EffectId, 3.0, 0.4)]
    [InlineData("Apocalypse", Explosivo.DamageEffectId, 3.0, 0.0)]
    [InlineData("Apocalypse", Explosivo.DamageEffectId, 3.0, 0.2)]
    [InlineData("Apocalypse", Explosivo.DamageEffectId, 3.0, 0.4)]
    [InlineData("Infernal Wave", CracklingInferno.EffectId, 3.0, 0.0)]
    [InlineData("Infernal Wave", CracklingInferno.EffectId, 3.0, 0.2)]
    [InlineData("Infernal Wave", CracklingInferno.EffectId, 3.0, 0.4)]
    public void Fire_Ball_Fire_Frogs_Explosivo_And_Crackling_Inferno_Tick_At_A_Fixed_Rhythm_Whatever_The_Haste(
        string ability, int effect, double period, double haste)
    {
        var f = ArdeosFor(ability, haste);

        f.Cast(ability);
        f.Wait(40); // the effect ticks out

        var gaps = GapsBetween(f.Ticks(effect)).Take(3).ToList();

        using var check = new Checks(f);
        check.True(gaps.Count >= 2);
        foreach (double gap in gaps) check.Equal(period, gap, GapTolerance);
    }

    /// <summary>
    /// Wildfire makes the burns of Fire Ball, Fire Frogs, Explosivo and Crackling Inferno tick 30% faster, at any haste: every period /
    /// 1.3.
    /// </summary>
    [Theory]
    [InlineData("Fire Ball", FireBallDot.EffectId, 2.0, 0.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 2.0, 0.2)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 2.0, 0.4)]
    [InlineData("Fire Frogs", FireFrogsDot.EffectId, 3.0, 0.0)]
    [InlineData("Fire Frogs", FireFrogsDot.EffectId, 3.0, 0.2)]
    [InlineData("Fire Frogs", FireFrogsDot.EffectId, 3.0, 0.4)]
    [InlineData("Apocalypse", Explosivo.DamageEffectId, 3.0, 0.0)]
    [InlineData("Apocalypse", Explosivo.DamageEffectId, 3.0, 0.2)]
    [InlineData("Apocalypse", Explosivo.DamageEffectId, 3.0, 0.4)]
    [InlineData("Infernal Wave", CracklingInferno.EffectId, 3.0, 0.0)]
    [InlineData("Infernal Wave", CracklingInferno.EffectId, 3.0, 0.2)]
    [InlineData("Infernal Wave", CracklingInferno.EffectId, 3.0, 0.4)]
    public void Wildfire_Makes_Fire_Ball_Fire_Frogs_Explosivo_And_Crackling_Inferno_Tick_30_Percent_Faster(
        string ability, int effect, double period, double haste)
    {
        var f = ArdeosFor(ability, haste);

        f.Cast(ability);
        double wildfireStart = f.Seconds();
        f.Cast<Wildfire>(); // as soon as the hero is free again
        f.Wait(40); // the effect ticks out

        double wildfireEnd = wildfireStart + 9.0; // Wildfire lasts 9 seconds
        var gaps = GapsBetween(f.Ticks(effect).Where(tick => tick.Time.TotalSeconds >= wildfireStart
                                                          && tick.Time.TotalSeconds <= wildfireEnd));

        using var check = new Checks(f);
        check.True(gaps.Count >= 2);
        foreach (double gap in gaps) check.Equal(period / 1.3, gap, GapTolerance);
    }

    // ---- Hasted rhythm: Searing Blaze, Engulfing Flames and Incinerate

    /// <summary>
    /// Searing Blaze (every 2 seconds), Engulfing Flames (1.5) and Incinerate's burn (3) tick faster with haste: every period /
    /// (1 + haste).
    ///
    /// Incinerate is the ultimate: its Spirit of Heroism adds 30% haste (Generic/Ultimate.cs), which its own burn counts.
    /// </summary>
    [Theory]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 2.0, 0.0, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 2.0, 0.2, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 2.0, 0.4, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 1.5, 0.0, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 1.5, 0.2, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 1.5, 0.4, 0.0)]
    [InlineData("Incinerate", IncinerateDot.EffectId, 3.0, 0.0, 0.3)]
    [InlineData("Incinerate", IncinerateDot.EffectId, 3.0, 0.2, 0.3)]
    [InlineData("Incinerate", IncinerateDot.EffectId, 3.0, 0.4, 0.3)]
    public void Searing_Blaze_Engulfing_Flames_And_Incinerate_Tick_Faster_With_Haste(string ability, int effect, double period,
        double haste, double spiritOfHeroism)
    {
        var f = ArdeosFor(ability, haste);

        f.Cast(ability);
        f.Wait(40); // the effect ticks out

        var gaps = GapsBetween(f.Ticks(effect)).Take(3).ToList();

        using var check = new Checks(f);
        check.True(gaps.Count >= 2);
        foreach (double gap in gaps) check.Equal(period / (1 + haste + spiritOfHeroism), gap, GapTolerance);
    }

    /// <summary>
    /// Wildfire makes Searing Blaze, Engulfing Flames and Incinerate's burn tick 30% faster on top of haste: every period / (1 + haste) /
    /// 1.3.
    ///
    /// Incinerate's Spirit of Heroism adds 30% haste, as in the test above.
    /// </summary>
    [Theory]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 2.0, 0.0, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 2.0, 0.2, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 2.0, 0.4, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 1.5, 0.0, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 1.5, 0.2, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 1.5, 0.4, 0.0)]
    [InlineData("Incinerate", IncinerateDot.EffectId, 3.0, 0.0, 0.3)]
    [InlineData("Incinerate", IncinerateDot.EffectId, 3.0, 0.2, 0.3)]
    [InlineData("Incinerate", IncinerateDot.EffectId, 3.0, 0.4, 0.3)]
    public void Wildfire_Makes_Searing_Blaze_Engulfing_Flames_And_Incinerate_Tick_30_Percent_Faster_On_Top_Of_Haste(
        string ability, int effect, double period, double haste, double spiritOfHeroism)
    {
        var f = ArdeosFor(ability, haste);

        f.Cast(ability);
        double wildfireStart = f.Seconds();
        f.Cast<Wildfire>(); // as soon as the hero is free again
        f.Wait(40); // the effect ticks out

        double wildfireEnd = wildfireStart + 9.0; // Wildfire lasts 9 seconds
        var gaps = GapsBetween(f.Ticks(effect).Where(tick => tick.Time.TotalSeconds >= wildfireStart
                                                          && tick.Time.TotalSeconds <= wildfireEnd));

        using var check = new Checks(f);
        check.True(gaps.Count >= 2);
        foreach (double gap in gaps) check.Equal(period / (1 + haste + spiritOfHeroism) / 1.3, gap, GapTolerance);
    }
}
