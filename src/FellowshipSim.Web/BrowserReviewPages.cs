using FellowshipSim.UI.Services;

namespace FellowshipSim.Web;

/// <summary>
/// Review pages in the browser app: no address serves them, so the review view reads each page's file from the data
/// folder through .NET (reviewView.js reads through .NET when the address does not load).
/// </summary>
public sealed class BrowserReviewPages : IReviewPages
{
    public string Url(string htmlFile) => "about:blank";
}
