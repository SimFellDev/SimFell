using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits Tariq's channel, arc and periodic effects make at several hastes, against the formulas: what is hasted follows haste,
/// what is fixed stays.
///
/// Every test plays the effect through and counts the events. The hastes avoid exact breakpoints (a tick landing on the end of an effect)
/// unless that is the point.
/// </summary>
public class TariqHasteBreakpoints
{
    // ---- Hammer Storm

    /// <summary>Hammer Storm always spins 3 times, whatever your haste: haste makes the spins come faster, it does not add spins.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Hammer_Storm_Spins_3_Times_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Tariq(haste: haste);
        f.Set(Tariq.Fury, 100); // enough to cast it

        f.Cast<HammerStorm>();
        f.Wait(2); // the spins are over

        Assert.Equal(3, f.Damage<HammerStorm>().Count);
    }

    // ---- Chain Lightning

    /// <summary>Chain Lightning always hits 6 times, whatever your haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Chain_Lightning_Hits_6_Times_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Tariq(haste: haste);

        f.Cast<ThunderCall>(); // enables Chain Lightning

        f.Cast<ChainLightning>();
        f.Wait(1); // the hits land

        Assert.Equal(6, f.Damage<ChainLightning>().Count);
    }

    // ---- Raging Tempest

    /// <summary>
    /// Raging Currents strikes every second, shortened by haste (Spirit of Heroism's 30% from Raging Tempest included): in 20 seconds it
    /// strikes floor(20 x (1.3 + haste)) times.
    ///
    /// At 2%, 27% and 72% haste: 26, 31 and 40 strikes.
    /// </summary>
    [Theory]
    [InlineData(0.02, 26)]
    [InlineData(0.27, 31)]
    [InlineData(0.72, 40)]
    public void Raging_Currents_Strikes_Follow_Haste(double haste, int strikes)
    {
        var f = StepFights.Tariq(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<RagingTempest>();
        f.Wait(21); // Raging Tempest is over

        Assert.Equal(strikes, f.Damage("Raging Currents").Count);
    }

    // ---- Bloodline

    /// <summary>
    /// Bloodline gives 1 Fury every second, shortened by haste: in 10 seconds floor(10 x (1 + haste)) Fury.
    /// </summary>
    [Theory]
    [InlineData(0.0, 10)]
    [InlineData(0.25, 12)]
    [InlineData(1.0, 20)]
    public void Bloodline_Fury_Follows_Haste(double haste, int fury)
    {
        var f = StepFights.Tariq(haste: haste, talents: [Bloodline.TalentId]);

        f.Wait(10.05);

        Assert.Equal(fury, f.Amount(Tariq.Fury), 1e-9);
    }
}
