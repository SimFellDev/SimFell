using System.Text.Json;

namespace FellowshipSim.Engine.Replay;

/// <summary>A replay review of one player in a raw fight: the replay script, the replay, and the review's files.</summary>
public static class ReplayReview
{
    /// <summary>
    /// Builds the player's replay script from the raw fight, replays it and renders the review.
    /// <paramref name="cancellationToken"/> stops it between steps with an <see cref="OperationCanceledException"/>.
    /// </summary>
    public static ReviewArtifacts Run(JsonElement raw, int playerId, ReplayOptions? options = null, IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var player = ReplayScriptBuilder.Players(raw).FirstOrDefault(p => p.Id == playerId)
            ?? throw new ArgumentException($"Player {playerId} did not take part in fight {new RawFightValue(raw)["fight"]["id"].Element}.");
        if (!ReplayDriver.SupportsHero(player.Hero))
        {
            throw new NotSupportedException($"{player.Name} plays {player.Hero}, which is not supported yet (supported: {string.Join(", ", ReplayDriver.SupportedHeroes)}).");
        }

        progress?.Report("Building the replay script");
        var scriptJson = ReplayScriptBuilder.Build(raw, playerId).ToJsonString();

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Replaying the fight");
        return Run(ReplayScript.FromJson(scriptJson), scriptJson, playerId, options, cancellationToken);
    }

    /// <summary>Replays a script and renders its review.</summary>
    public static ReviewArtifacts Run(ReplayScript script, string scriptJson, int playerId, ReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ReplayOptions { StopOnFirstFailure = false };
        var hero = ReplayDriver.CreateHero(script);
        var result = new ReplayDriver(hero, script, options).Run(cancellationToken);
        return Artifacts(result, script, scriptJson, playerId);
    }

    /// <summary>The review's files for a finished replay of <paramref name="script"/>.</summary>
    public static ReviewArtifacts Artifacts(ReplayResult result, ReplayScript script, string scriptJson, int playerId)
    {
        string id = ReplayLibrary.ReviewId(script.ReportCode, script.FightId, playerId);
        var (engineDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
        var summary = new ReviewSummary(id, script.ReportCode, script.FightId, script.FightName, script.FightDate, playerId,
            result.PlayerName, script.Player.Hero, result.PassedSteps, result.Steps.Count, result.MatchedLogHits, result.LogHits,
            engineDamage, logDamage, result.ForcedCasts.Count, DateTime.UtcNow);

        return new ReviewArtifacts(summary, scriptJson, ReplayReportWriter.Render(result), ReplayReportWriter.Summary(result), HitsCsv(result));
    }

    private static string HitsCsv(ReplayResult result)
    {
        var sb = new System.Text.StringBuilder("time,ability_id,ability,target,engine_amount,expected_low,expected_high,engine_crit,crit_chance,log_amount,log_crit,log_time,deviation,tick_fraction,player_auras,target_auras\n");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var h in result.AllHits)
        {
            string name = h.AbilityName.Replace(',', ' ');
            sb.Append(string.Format(inv, "{0:F3},{1},{2},{3},{4:F1},{5:F1},{6:F1},{7},{8:F4},{9},{10},{11},{12:F1},{13:F3},{14},{15}\n",
                h.EngineTime, h.AbilityId, name, h.Target == null ? "" : $"{h.Target.Id}.{h.Target.Instance}",
                h.EngineAmount, h.ExpectedLow, h.ExpectedHigh, h.Crit ? 1 : 0, h.CritChance,
                h.Log == null ? "" : h.Log.UnblockedAmount.ToString("F0", inv), h.Log == null ? "" : (h.Log.Crit ? "1" : "0"),
                h.Log == null ? "" : h.Log.Time.ToString("F3", inv), h.Deviation, h.TickFraction,
                AuraColumn(h.State?.Hero.Auras), AuraColumn(h.State?.Target?.Auras)));
        }
        return sb.ToString();
    }

    /// <summary>The engine's auras at the hit: log IDs, then engine-only auras by name.</summary>
    private static string AuraColumn(IReadOnlyList<EngineAura>? auras) => auras == null ? "" : string.Join(" ",
        auras.Where(a => a.LogId > 0).Select(a => a.LogId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Concat(auras.Where(a => a.LogId <= 0).Select(a => a.Name.Replace(' ', '_').Replace(',', '_'))));
}
