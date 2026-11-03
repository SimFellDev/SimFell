namespace FellowshipSim.Core.Engine;

/// <summary>
/// How long a player takes to cast on the global cooldown once nothing holds them, as SimulationCraft's channel_lag has
/// it. A cast queued during the global cooldown or a cast time fires as it ends (in game: a spell queued during the
/// global cooldown goes off as it ends), so neither has a delay. The combat logs have that cast 0.001s after a cast time
/// and a median 0.050s after the global cooldown, the server frame it falls in, which the engine does not model; the
/// engine has none for either. Nothing is queued out of a channel, so the player reacts: log-normal around the median,
/// since the logs' delays have a long tail a normal distribution does not fit. Combat logs (tests/ReactionTimeTests,
/// 29 Rime fights of patch 0.5.0.0, delays under 1s): after a channel the median is 0.171s (quartiles 0.101 and 0.336,
/// 10th and 90th percentiles 0.038 and 0.539).
/// </summary>
public sealed record ReactionDelays(
    double GlobalCooldownMedian, double GlobalCooldownSigma,
    double CastTimeMedian, double CastTimeSigma,
    double ChannelMedian, double ChannelSigma)
{
    /// <summary>The players of the combat logs.</summary>
    public static readonly ReactionDelays Measured = new(
        GlobalCooldownMedian: 0.0, GlobalCooldownSigma: 0.0,
        CastTimeMedian: 0.0, CastTimeSigma: 0.0,
        ChannelMedian: 0.171, ChannelSigma: 0.89);

    /// <summary>
    /// The delay after <paramref name="hold"/> for two uniform numbers in [0, 1): the median times e^(sigma z), z standard
    /// normal by Box-Muller. A sigma of a quartile spread is ln(Q3 / Q1) / 1.349.
    /// </summary>
    public SimTime Delay(ReactionHold hold, double u1, double u2)
    {
        var (median, sigma) = hold switch
        {
            ReactionHold.GlobalCooldown => (GlobalCooldownMedian, GlobalCooldownSigma),
            ReactionHold.CastTime => (CastTimeMedian, CastTimeSigma),
            _ => (ChannelMedian, ChannelSigma),
        };
        if (median <= 0) return SimTime.Zero;
        double z = Math.Sqrt(-2.0 * Math.Log(1.0 - u1)) * Math.Cos(2.0 * Math.PI * u2);
        return SimTime.FromSeconds(median * Math.Exp(sigma * z));
    }
}
