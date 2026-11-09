using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits Gunde's damage over time effects and pulses deal at each haste. Rend, Slaughter's bleed, Grim Carve's spins and Bloodbound
/// Spirit's pulses are not hasted: their counts are the same at any haste (the one thing haste changes is how often you can cast). Every
/// test casts once at 0%, 30% and 100% haste and counts the hits.
/// </summary>
public class GundeHasteBreakpoints
{
    // ---- Rend

    /// <summary>Rend deals 10 ticks per application, whatever your haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Rend_Has_10_Ticks_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste);

        f.Cast<Rupture>();
        f.Wait(31); // every tick of the Rend

        Assert.Equal(10, f.Damage("Rend").Count);
    }

    // ---- Slaughter

    /// <summary>Slaughter's bleed deals 10 ticks, whatever your haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Slaughter_Has_10_Ticks_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste);

        f.Cast<ReaversEdge>(); // Rend for Slaughter to consume

        f.Cast<Slaughter>();
        f.Wait(4); // every tick of the bleed

        Assert.Equal(10, f.Ticks(Slaughter.DotEffectId).Count);
    }

    // ---- Grim Carve and Bloodbound Spirit

    /// <summary>Grim Carve spins 3 times and Bloodbound Spirit pulses 5 times, whatever your haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Spins_And_Pulses_Do_Not_Change_With_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast<GrimCarve>();
        f.Cast<BloodboundSpirit>();
        f.Wait(8); // every spin and pulse

        using var check = new Checks(f);
        check.Equal(3, f.Damage("Grim Carve").Count);
        check.Equal(5, f.Damage("Bloodbound Spirit").Count);
    }
}
