using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits Gunde's damage over time and pulses give at different haste values. Rend, Slaughter's bleed, Grim Carve's spins and Bloodbound Spirit's
/// pulses are not hasted: their counts are the same at any haste (the one thing haste changes is how often you can cast).
/// Dev: each is cast once at 0%, 30% and 100% haste and counted.
/// </summary>
public class GundeHasteBreakpoints
{
    /// <summary>
    /// Player: Rend deals 10 ticks per application whatever your haste.
    /// Dev: a Rupture; 31 seconds; 10 ticks.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Rend_Has_10_Ticks_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste);
        f.Cast<Rupture>();

        f.Wait(31);

        Assert.Equal(10, f.Damage("Rend").Count);
    }

    /// <summary>
    /// Player: Slaughter's bleed deals 10 ticks whatever your haste.
    /// Dev: a Reaver's Edge then Slaughter; 4 seconds; 10 ticks of its effect.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Slaughter_Has_10_Ticks_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste);
        f.Cast<ReaversEdge>();
        f.Cast<Slaughter>();

        f.Wait(4);

        Assert.Equal(10, f.DamageOfEffect(3237).Count);
    }

    /// <summary>
    /// Player: Grim Carve spins 3 times and Bloodbound Spirit pulses 5 times whatever your haste.
    /// Dev: one cast of each; the hits counted.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Spins_And_Pulses_Do_Not_Change_With_Haste(double haste)
    {
        var f = StepFights.Gunde(haste);
        f.GiveGundeSpirit();

        f.Cast<GrimCarve>();
        f.Cast<BloodboundSpirit>();
        f.Wait(8);

        Assert.Equal(3, f.Damage("Grim Carve").Count);
        Assert.Equal(5, f.Damage("Bloodbound Spirit").Count);
    }
}
