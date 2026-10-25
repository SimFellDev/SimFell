namespace FellowshipSim.UI.Services;

/// <summary>
/// Address of each review page in <see cref="Logs.ReplayLibrary.ReviewsDirectory"/>. Each host serves the folder its own
/// way, so the host registers the implementation.
/// </summary>
public interface IReviewPages
{
    /// <summary>Address of the review page <paramref name="htmlFile"/>, a file name in the reviews folder.</summary>
    string Url(string htmlFile);
}
