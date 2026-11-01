using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Engine;

/// <summary>
/// The simulation engine as the app uses it: heroes, the action-list grammar, replay reviews of logged fights and Monte
/// Carlo sims. <see cref="LocalSimEngine"/> runs it in this process. Every argument and result survives a JSON round trip
/// (<see cref="System.Text.Json.JsonSerializerOptions.Web"/>), so an implementation can call an engine server.
/// A request the engine refuses throws <see cref="SimEngineException"/> with the reason.
/// </summary>
public interface ISimEngine
{
    /// <summary>The heroes the engine can simulate, replay and write action lists for, in name order.</summary>
    Task<IReadOnlyList<string>> GetHeroesAsync(CancellationToken ct = default);

    /// <summary>
    /// The abilities, resources, auras, values and properties an action list for <paramref name="hero"/> can use, or null
    /// when the engine has no such hero.
    /// </summary>
    Task<AplVocabulary?> GetVocabularyAsync(string hero, CancellationToken ct = default);

    /// <summary>
    /// Tokens of the expression <paramref name="text"/> with their kinds for <paramref name="hero"/>, and its parse error;
    /// null when the engine has no such hero.
    /// </summary>
    Task<AplExpressionCheck?> CheckExpressionAsync(string hero, string text, CancellationToken ct = default);

    /// <summary>The actions of an action list's text. Text outside the grammar throws <see cref="SimEngineException"/>.</summary>
    Task<AplDocumentActions> ParseActionListAsync(string text, CancellationToken ct = default);

    /// <summary>The text of an action list's actions, or the problem with each field that stops it being written.</summary>
    Task<AplDocumentText> WriteActionListAsync(IReadOnlyList<AplDocumentAction> actions, CancellationToken ct = default);

    /// <summary>
    /// Replays player <paramref name="playerId"/> of a raw fight (JSON as downloaded from FellowshipLogs) and returns the
    /// review's files. A player missing from the fight, or of a hero the replay does not support, throws
    /// <see cref="SimEngineException"/>.
    /// </summary>
    Task<ReviewArtifacts> ReviewAsync(Stream rawFight, int playerId, IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Monte Carlo fights of <paramref name="profile"/>'s build casting by its action list. <paramref name="progress"/>
    /// receives completed fights. Cancelling <paramref name="ct"/> stops the fights with an
    /// <see cref="OperationCanceledException"/>. An action list outside the grammar, or naming an ability the hero does not
    /// have, throws <see cref="SimEngineException"/>.
    /// </summary>
    Task<SimResult> SimulateAsync(SimcProfile profile, SimSettings settings, IProgress<SimProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// The first fight of a sim of <paramref name="profile"/> with <paramref name="settings"/> (seed
    /// <see cref="SimSettings.FirstSeed"/>), with every decision of its action list: the hero and enemies, each step's
    /// values and the cast. It travels as JSON in <see cref="SimChunks.Json"/>. An action list outside the grammar, or
    /// naming an ability the hero does not have, throws <see cref="SimEngineException"/>.
    /// </summary>
    Task<AplFightTrace> TraceFightAsync(SimcProfile profile, SimSettings settings, CancellationToken ct = default);
}
