using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using TariqTalents = FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits a channel, an arc or a periodic effect makes at several haste values, against the formulas: what is hasted follows haste, what is
/// fixed stays.
/// Dev: each test plays the effect through and counts the events. Hastes avoid exact breakpoints (a tick landing on the end of an effect) unless that is
/// the point.
/// </summary>
public class TariqHasteBreakpoints
{
    /// <summary>
    /// Player: Hammer Storm always spins 3 times, whatever your haste: haste makes the spins come faster, it does not add spins.
    /// Dev: at 0%, 30% and 100% haste; one cast; 3 spins.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Hammer_Storm_Spins_3_Times_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Tariq(haste);
        f.SetFury(100);

        f.Cast<HammerStorm>();
        f.Wait(2);

        Assert.Equal(3, f.Damage("Hammer Storm").Count);
    }

    /// <summary>
    /// Player: Chain Lightning always hits 6 times, 0.3 seconds after the cast and 0.05 seconds apart, whatever your haste.
    /// Dev: at 0%, 30% and 100% haste; Thunder Call; one cast; 6 hits, the first 0.3s after the cast, the last 0.55s after (20ms leeway).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void Chain_Lightning_Hits_6_Times_At_The_Same_Times_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Tariq(haste);
        f.Cast<ThunderCall>();
        var start = f.Now.TotalSeconds;

        f.Cast<ChainLightning>();
        f.Wait(1);

        var hits = f.Damage("Chain Lightning");
        Assert.Equal(6, hits.Count);
        Assert.Equal(start + 0.30, hits[0].Time.TotalSeconds, 0.02);
        Assert.Equal(start + 0.55, hits[5].Time.TotalSeconds, 0.02);
    }

    /// <summary>
    /// Player: Raging Currents strikes every second, shortened by haste (Raging Tempest's own 30% included): in 20 seconds it strikes 20 x (1 + haste) times,
    /// rounded down.
    /// Dev: at 2%, 27% and 72% haste; Raging Tempest; 21 seconds pass; strikes are floor(20 x (1.3 + haste)) = 26, 31 and 40.
    /// </summary>
    [Theory]
    [InlineData(0.02, 26)]
    [InlineData(0.27, 31)]
    [InlineData(0.72, 40)]
    public void Raging_Currents_Strikes_Follow_Haste(double haste, int strikes)
    {
        var f = StepFights.Tariq(haste);
        f.GiveTariqSpirit();

        f.Cast<RagingTempest>();
        f.Wait(21);

        Assert.Equal(strikes, f.Damage("Raging Currents").Count);
    }

    /// <summary>
    /// Player: Bloodline gives 1 Fury every second, shortened by haste: in 10 seconds 10 x (1 + haste) Fury, rounded down.
    /// Dev: at 0%, 25% and 100% haste; 10.05 seconds in combat doing nothing; Fury is 10, 12 and 20 (the tick at exactly 10s counts at 0% and 100%).
    /// </summary>
    [Theory]
    [InlineData(0.0, 10)]
    [InlineData(0.25, 12)]
    [InlineData(1.0, 20)]
    public void Bloodline_Fury_Follows_Haste(double haste, int fury)
    {
        var f = StepFights.Tariq(haste, talents: [TariqTalents.Bloodline.TalentId]);

        f.Wait(10.05);

        Assert.Equal(fury, f.Fury(), 1e-9);
    }
}
