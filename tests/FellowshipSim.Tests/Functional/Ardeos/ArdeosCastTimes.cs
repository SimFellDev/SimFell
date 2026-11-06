using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Ardeos ability keeps you busy: from the start of the cast until you can act again (cast or channel over, and the
/// global cooldown).
/// </summary>
public class ArdeosCastTimes
{
    // ---- Instants

    /// <summary>An instant ability keeps you busy for the 1.5 second global cooldown, shortened by haste.</summary>
    [Theory]
    [InlineData("Searing Blaze", 0.0)]
    [InlineData("Searing Blaze", 0.2)]
    [InlineData("Fire Ball", 0.0)]
    [InlineData("Fire Ball", 0.2)]
    [InlineData("Fire Frogs", 0.0)]
    [InlineData("Fire Frogs", 0.2)]
    public void An_Instant_Takes_The_Global_Cooldown_Of_1_5_Seconds_Over_1_Plus_Haste(string ability, double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast(ability);

        AssertSeconds(1.5 / (1 + haste), f.Seconds());
    }

    // ---- Engulfing Flames

    /// <summary>Engulfing Flames takes a 1.5 second cast, shortened by haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Engulfing_Flames_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<EngulfingFlames>();

        AssertSeconds(1.5 / (1 + haste), f.Seconds());
    }

    // ---- Detonate

    /// <summary>
    /// Detonate keeps you busy for the same time whatever your haste: 31/30 seconds, or 29/30 seconds when you wear a legendary item (its
    /// +10% cooldown recovery).
    ///
    /// The legendary item is Explosivo's helm. The engine's 31 and 29 server frames come from the combat logs (Detonate.cs).
    /// </summary>
    [Theory]
    [InlineData(0.0, false, 31.0 / 30.0)]
    [InlineData(0.1, false, 31.0 / 30.0)]
    [InlineData(0.2, false, 31.0 / 30.0)]
    [InlineData(0.0, true, 29.0 / 30.0)]
    [InlineData(0.1, true, 29.0 / 30.0)]
    [InlineData(0.2, true, 29.0 / 30.0)]
    public void Detonate_Takes_A_Fixed_Time_Whatever_Your_Haste(double haste, bool legendary, double busy)
    {
        var f = StepFights.Ardeos(haste: haste, legendaries: legendary ? [Explosivo.HelmItemId] : null);

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
        double start = f.Seconds();

        f.Cast<Detonate>();

        AssertSeconds(start + busy, f.Seconds());
    }

    // ---- Incinerate (its hits at each haste are in ArdeosHasteBreakpoints.cs)

    /// <summary>Incinerate takes a 1.5 second cast, shortened by haste, then a 2.5 second channel that haste does not shorten.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Incinerate_Takes_A_1_5_Second_Cast_Over_1_Plus_Haste_Then_A_2_5_Second_Channel(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<Incinerate>();

        using var check = new Checks(f);
        check.Seconds(1.5 / (1 + haste), f.CompletedAt("Incinerate"));
        check.Seconds(1.5 / (1 + haste) + 2.5, f.Seconds());
    }
}
