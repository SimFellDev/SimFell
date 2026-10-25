using FellowshipSim.Logs;

namespace FellowshipSim.UI.Shell;

/// <summary>Titles of the review pages of one review.</summary>
public static class ReviewTitle
{
    public static string Replay(ReviewSummary review) => $"Review: {review.PlayerName} in {review.FightName}";

    public static string FreeDice(ReviewSummary review) => $"Free dice: {review.PlayerName} in {review.FightName}";
}
