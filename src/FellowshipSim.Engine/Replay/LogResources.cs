using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Tariq;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// How combat logs report each hero's resources, so a replay can compare them with the engine before every cast. A
/// resource not listed here is not compared: add a hero's resources when adding the hero.
/// </summary>
public static class LogResources
{
    /// <summary>
    /// <paramref name="LogResourceType"/> is the log's resource type number, <paramref name="Scale"/> converts log values to
    /// engine units and <paramref name="Tolerance"/> is how far the engine may differ before the replay reports it. With
    /// <paramref name="StartsFromLog"/> the replay starts the resource at the log's first reading (it carries over from
    /// earlier pulls of the dungeon). With <paramref name="ShareOfMaximum"/> the log's amount is a share of the log's
    /// maximum, and the engine amount is that share of the engine's maximum (for a maximum that changes during the fight,
    /// e.g. Tariq's Fury).
    /// </summary>
    public sealed record LogResource(ResourceDef Resource, int LogResourceType, double Scale = 1.0, double Tolerance = 0.0, bool StartsFromLog = false,
        bool ShareOfMaximum = false)
    {
        /// <summary>The engine amount a log reading stands for.</summary>
        public double EngineAmount(LogResourceValue value) =>
            ShareOfMaximum ? (value.Max > 0 ? value.Amount / value.Max * Resource.Max : 0.0) : value.Amount * Scale;
    }

    public static readonly LogResource[] All =
    [
        // Every hero: Spirit Points as resource type 4, in hundredths; they carry over between pulls.
        new(Hero.SpiritPoints, 4, Scale: 0.01, Tolerance: 2.0, StartsFromLog: true),
        // Rime: Anima as resource type 2 and Winter Orbs as type 5, both in hundredths. Anima is compared within 0.99
        // (the log rounds it).
        new(Rime.Anima, 2, Scale: 0.01, Tolerance: 0.99),
        new(Rime.WinterOrbsResource, 5, Scale: 0.01),
        // Ardeos: Cinders as resource type 2, in hundredths (max 40000).
        new(Ardeos.Cinders, 2, Scale: 0.01, Tolerance: 1.0),
        // Mara: Energy as resource type 2 and Combo Points as type 3, both in hundredths; both carry over between pulls.
        // Energy regenerates between the engine's ticks, so it is compared within 3.
        new(Mara.Energy, 2, Scale: 0.01, Tolerance: 3.0, StartsFromLog: true),
        new(Mara.ComboPoints, 3, Scale: 0.01, StartsFromLog: true),
        // Tariq: Fury as resource type 2, a share of a maximum that follows Strength. Compared within 2 (Bloodline and Ride
        // the Lightning tick between readings).
        new(Tariq.Fury, 2, Tolerance: 2.0, StartsFromLog: true, ShareOfMaximum: true),
    ];
}
