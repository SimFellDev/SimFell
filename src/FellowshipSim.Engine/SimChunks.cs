using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using Superpower;

namespace FellowshipSim.Engine;

/// <summary>
/// One sim split into chunks of fights (<see cref="MonteCarloSimulator.RunChunk"/>), for an engine that runs them in
/// other processes, such as the browser app's workers: each runs some chunks, and <see cref="Combine"/> merges them all
/// to the result <see cref="LocalSimEngine.SimulateAsync"/> gives. Chunks travel as JSON in <see cref="Json"/>.
/// </summary>
public sealed class SimChunks
{
    /// <summary>The JSON of chunks and results: the web defaults, with an empty chunk's infinite minimum and maximum.</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerOptions.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly MonteCarloSimulator _simulator;
    private readonly SimSettings _settings;

    /// <summary>
    /// The sim of <paramref name="profile"/> with <paramref name="settings"/>. An action list outside the grammar, or naming
    /// an ability the hero does not have, throws <see cref="SimEngineException"/>.
    /// </summary>
    public SimChunks(SimcProfile profile, SimSettings settings)
    {
        CheckDungeon(settings);
        _simulator = NewSimulator(profile);
        _settings = settings;
    }

    /// <summary>The chunks of a sim with <paramref name="settings"/>.</summary>
    public static int Count(SimSettings settings) => MonteCarloSimulator.ChunkCount(settings.Iterations);

    /// <summary>The fights of chunk <paramref name="chunk"/>, and their totals.</summary>
    public MonteCarloChunk Run(int chunk, CancellationToken ct = default) =>
        _simulator.RunChunk(chunk, SimTime.FromSeconds(_settings.DurationSeconds), _settings.Iterations, _settings.TargetCount,
            _settings.FirstSeed, cancellationToken: ct, fight: _settings.Fight);

    /// <summary>The sim's result from every one of its chunks, in chunk order, with its typical fight rolled as <see cref="MonteCarloSimulator.Run"/> has it.</summary>
    public SimResult Combine(IReadOnlyList<MonteCarloChunk> chunks)
    {
        var duration = SimTime.FromSeconds(_settings.DurationSeconds);
        var combined = MonteCarloSimulator.Combine(chunks, duration, _settings.Iterations);
        var result = SimResult.From(combined);
        if (combined.TypicalFight is not { } typical) return result;
        var rolled = _simulator.RollFight(typical, duration, _settings.TargetCount, _settings.FirstSeed, _settings.Fight);
        return result with { Casts = rolled.Casts, Hits = rolled.Hits };
    }

    /// <summary>Throws <see cref="SimEngineException"/> when <paramref name="settings"/> has a dungeon that is not valid.</summary>
    public static void CheckDungeon(SimSettings settings)
    {
        if (settings.Dungeon?.Problem() is { } problem) throw new SimEngineException(problem);
    }

    /// <summary>
    /// A simulator of <paramref name="profile"/>'s build casting by its action list; an action list it cannot run throws
    /// <see cref="SimEngineException"/>.
    /// </summary>
    public static MonteCarloSimulator NewSimulator(SimcProfile profile)
    {
        try
        {
            return new MonteCarloSimulator(profile.Build, AplParser.ParseScript(profile.ActionList));
        }
        catch (Exception e) when (e is ParseException or InvalidOperationException)
        {
            throw new SimEngineException(e.Message, e);
        }
    }
}
