using System.Text.Json;
using System.Text.Json.Nodes;
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

    private async Task<JsonObject?> CombatantInfoAsync(string reportCode, int fightId, int playerId,
        IProgress<string>? progress, CancellationToken ct)
    {
        if (library.HasRawFight(reportCode, fightId))
        {
            // The fight's JsonDocument, read in place: its events as a JsonNode tree are over a GB for a long fight.
            using var fight = await library.GetRawFightDocumentAsync(null, reportCode, fightId, progress, ct);
            if (!fight.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array) return null;
            foreach (var e in events.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.Object
                    && e.TryGetProperty("type", out var type) && type.ValueEquals("combatantinfo")
                    && e.TryGetProperty("sourceID", out var source) && source.ValueKind == JsonValueKind.Number
                    && source.TryGetInt32(out int id) && id == playerId)
                {
                    return JsonNode.Parse(e.GetRawText())!.AsObject();
                }
            }
            return null;
        }
        var client = await settings.CreateClientAsync(ct)
            ?? throw new FellowshipLogsException("Sign in with FellowshipLogs to import characters.");
        progress?.Report("Reading the character from FellowshipLogs");
        return await client.GetCombatantInfoAsync(reportCode, fightId, playerId, ct);
    }
}
