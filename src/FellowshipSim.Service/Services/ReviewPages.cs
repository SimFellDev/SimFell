using FellowshipSim.UI.Services;

namespace FellowshipSim.Service.Services;

/// <summary>Review pages at <c>reviews/</c>, relative to the page's base address. <see cref="ServiceApp"/> serves the folder there.</summary>
public sealed class ReviewPages : IReviewPages
{
    public string Url(string htmlFile) => $"reviews/{Uri.EscapeDataString(htmlFile)}";
}
