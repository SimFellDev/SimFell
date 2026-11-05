namespace FellowshipSim.Engine.Replay;

/// <summary>A replay review of one player in a raw fight: the replay script, the replay, and the review's files.</summary>
public static class ReplayReview
{
    /// <summary>
    /// Builds the player's replay script from a raw fight's UTF-8 JSON, replays it and renders the review.
    /// <paramref name="cancellationToken"/> stops it between steps with an <see cref="OperationCanceledException"/>. A raw
    /// fight that is not a JSON object throws <see cref="ArgumentException"/>.
    /// </summary>
    public static ReviewArtifacts Run(ReadOnlyMemory<byte> rawFight, int playerId, ReplayOptions? options = null,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        Run(new RawFightHolder(rawFight), playerId, options, progress, cancellationToken);

    /// <summary>
    /// <see cref="Run(ReadOnlyMemory{byte}, int, ReplayOptions?, IProgress{string}?, CancellationToken)"/> for the raw fight
    /// in <paramref name="rawFight"/>, which the script's build empties: nothing keeps the raw fight through the replay.
    /// </summary>
    public static ReviewArtifacts Run(RawFightHolder rawFight, int playerId, ReplayOptions? options = null,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        Replay(ScriptJson(rawFight, playerId, progress), playerId, options, progress, cancellationToken);

    // The replay script's UTF-8 JSON. The raw fight is held by this method's frame alone, so it is free once the script is
    // built.
    private static byte[] ScriptJson(RawFightHolder rawFight, int playerId, IProgress<string>? progress)
    {
        var raw = rawFight.Take();
        var players = ReplayScriptBuilder.Players(raw.Span);
        var player = players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new ArgumentException($"Player {playerId} did not take part in fight {RawFightHead.Read(raw.Span)["fight"]["id"].Element}.");
        if (!ReplayDriver.SupportsHero(player.Hero))
        {
            throw new NotSupportedException($"{player.Name} plays {player.Hero}, which is not supported yet (supported: {string.Join(", ", ReplayDriver.SupportedHeroes)}).");
        }

        progress?.Report("Building the replay script");
        return ReplayScriptBuilder.Build(raw, playerId);
    }

    private static ReviewArtifacts Replay(byte[] scriptJson, int playerId, ReplayOptions? options, IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Replaying the fight");
        return Run(ReplayScript.FromJson(scriptJson), scriptJson, playerId, options, cancellationToken);
    }

    /// <summary>Replays a script and renders its review.</summary>
    public static ReviewArtifacts Run(ReplayScript script, byte[] scriptJson, int playerId, ReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ReplayOptions { StopOnFirstFailure = false };
        var hero = ReplayDriver.CreateHero(script);
        var result = new ReplayDriver(hero, script, options).Run(cancellationToken);
        return Artifacts(result, script, scriptJson, playerId);
    }

    /// <summary>The review's files for a finished replay of <paramref name="script"/>, whose UTF-8 JSON is <paramref name="scriptJson"/>.</summary>
    public static ReviewArtifacts Artifacts(ReplayResult result, ReplayScript script, byte[] scriptJson, int playerId)
    {
        string id = ReplayLibrary.ReviewId(script.ReportCode, script.FightId, playerId);
        var (engineDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
        var summary = new ReviewSummary(id, script.ReportCode, script.FightId, script.FightName, script.FightDate, playerId,
            result.PlayerName, script.Player.Hero, result.PassedSteps, result.Steps.Count, result.MatchedLogHits, result.LogHits,
            engineDamage, logDamage, result.ForcedCasts.Count, DateTime.UtcNow);

        return new ReviewArtifacts(summary, scriptJson, ReplayReportWriter.Render(result), ReplayReportWriter.Summary(result), HitsCsv(result),
            ReplayFindingsDocument.ForFight(new ReviewedFight(result, script, playerId)));
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
