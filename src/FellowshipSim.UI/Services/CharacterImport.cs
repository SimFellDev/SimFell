using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// A player's character as a logged fight shows it: the fight's <c>combatantinfo</c> event read by
/// <see cref="CharacterReader"/>. A fight already downloaded for a replay (<see cref="ReplayLibrary"/>) is read from disk;
/// otherwise FellowshipLogs is asked for that one event alone, not the fight's every event.
/// </summary>
public sealed class CharacterImport(ReplayLibrary library, LogsSettingsStore settings)
{
    /// <summary>
    /// The build of player <paramref name="player"/> in fight <paramref name="fightId"/> of report <paramref name="reportCode"/>.
    /// Throws <see cref="FellowshipLogsException"/> when FellowshipLogs cannot be asked, and
    /// <see cref="InvalidOperationException"/> when the fight has no character information for the player.
    /// </summary>
    public async Task<CharacterBuild> ReadAsync(string reportCode, int fightId, FightPlayer player,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var combatant = await CombatantInfoAsync(reportCode, fightId, player.Id, progress, ct)
            ?? throw new InvalidOperationException($"Fight {fightId} has no character information for {player.Name}.");
        return CharacterReader.FromCombatantInfo(combatant, player.Name, BuildHeroes.Find(player.Hero) ?? player.Hero);
    }

    private async Task<System.Text.Json.Nodes.JsonObject?> CombatantInfoAsync(string reportCode, int fightId, int playerId,
        IProgress<string>? progress, CancellationToken ct)
    {
        if (library.HasRawFight(reportCode, fightId))
        {
            var raw = await library.GetRawFightAsync(null, reportCode, fightId, progress, ct);
            return raw["events"]?.AsArray()
                .FirstOrDefault(e => e?["type"]?.GetValue<string>() == "combatantinfo" && e["sourceID"]?.GetValue<int>() == playerId)
                ?.AsObject();
        }
        var client = await settings.CreateClientAsync(ct)
            ?? throw new FellowshipLogsException("Sign in with FellowshipLogs to import characters.");
        progress?.Report("Reading the character from FellowshipLogs");
        return await client.GetCombatantInfoAsync(reportCode, fightId, playerId, ct);
    }
}
