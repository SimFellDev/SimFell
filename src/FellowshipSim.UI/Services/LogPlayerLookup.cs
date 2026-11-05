using FellowshipSim.Core.Import;
using FellowshipSim.Logs;
using FellowshipSim.UI.Replay;

namespace FellowshipSim.UI.Services;

/// <summary>The players of a hero in a FellowshipLogs report link or on a character's page, for a build read from a log.</summary>
public sealed class LogPlayerLookup(ReportLookup reports, LogsSettingsStore settings)
{
    /// <summary>
    /// The players of <paramref name="hero"/> that <paramref name="link"/> names: a character page's player in each of its
    /// recent fights, the latest first; a report link's player, or every player of the hero in its fight or report. Throws
    /// <see cref="ArgumentException"/> for text that is no link, <see cref="FellowshipLogsException"/> when FellowshipLogs
    /// cannot be asked, and <see cref="InvalidOperationException"/> when the link has no player of the hero.
    /// </summary>
    public async Task<LogPlayers> FindAsync(string link, string hero, CancellationToken ct = default)
    {
        if (CharacterLink.TryParse(link) is { } page)
        {
            var client = await settings.CreateClientAsync(ct) ?? throw new FellowshipLogsException("Sign in with FellowshipLogs first.");
            var character = await client.GetCharacterAsync(page.CharacterId, ct);
            if (character.Fights.Count == 0) throw new InvalidOperationException($"{character.Name} has no recent fights on FellowshipLogs.");
            var fights = character.Fights.Where(f => IsHero(f.Player.Hero, hero)).ToList();
            if (fights.Count == 0) throw new InvalidOperationException($"{character.Name}'s recent fights have no {hero}.");
            return new LogPlayers(fights.Select(f => new PickedPlayer(
                f.ReportCode,
                new ReportFight(f.FightId, f.FightName, f.Kill, f.DurationSeconds, [Player(f.Player)]),
                Player(f.Player),
                GamePatch.Current.Covers(DateTimeOffset.FromUnixTimeMilliseconds(f.ReportStartTime).UtcDateTime, f.LogVersion))).ToList(), page);
        }
        var report = await reports.FindAsync(link, ct);
        var players = report.Fights
            .Where(f => report.SelectedFightId == null || f.Id == report.SelectedFightId)
            .SelectMany(f => f.Players
                .Where(p => IsHero(p.Hero, hero) && (report.SelectedPlayerId == null || p.Id == report.SelectedPlayerId))
                .Select(p => new PickedPlayer(report.Code, f, p, report.CurrentPatch)))
            .Reverse()
            .ToList();
        if (players.Count == 0)
        {
            throw new InvalidOperationException(report.SelectedPlayerId != null
                ? $"The link's player is not a {hero} in the fight."
                : $"The {(report.SelectedFightId != null ? "fight" : "report")} has no {hero}.");
        }
        return new LogPlayers(players, null);
    }

    private static FightPlayer Player(ReportPlayer player) => new(player.Id, player.Name, player.Hero, true, false);

    // The log's hero name, as the build editor names it.
    private static bool IsHero(string logged, string hero) => (BuildHeroes.Find(logged) ?? logged).Equals(hero, StringComparison.OrdinalIgnoreCase);
}
