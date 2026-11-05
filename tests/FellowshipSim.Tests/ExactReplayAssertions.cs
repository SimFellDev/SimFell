using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>What every exact replay of a whole fight shows, whatever the engine gets wrong.</summary>
internal static class ExactReplayAssertions
{
    public static void Hold(ReplayResult result)
    {
        Assert.True(result.MatchedLogHits > 0);
        // Every log hit is dealt once: matched by an engine hit, or by the replay itself.
        Assert.Equal(result.LogHits, result.MatchedLogHits + result.LoggedHitsDealt);
        // The replay deals the log's damage.
        double logTotal = result.LogDamage.Sum(d => d.UnblockedAmount);
        Assert.Equal(logTotal, result.DamageDealt, Math.Max(1e-6, logTotal * 1e-9));
        // The findings hold every issue once.
        Assert.Equal(result.Issues.Count, ReplayFindings.From(result).Sum(f => f.Issues.Count));
        foreach (var issue in result.Issues.Where(i => i.Expected != null))
        {
            Assert.True(issue.Observed.HasValue, issue.ToString());
        }
        // The checks with a numeric range always have one.
        foreach (var issue in result.Issues.Where(i => i.Category is IssueCategory.Damage or IssueCategory.Ticks or IssueCategory.GlobalCooldown
                     or IssueCategory.Cooldown or IssueCategory.Resources or IssueCategory.Auras))
        {
            Assert.True(issue.Expected != null && issue.Observed.HasValue, issue.ToString());
        }
    }
}
