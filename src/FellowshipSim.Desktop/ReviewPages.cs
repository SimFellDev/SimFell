using FellowshipSim.Logs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Desktop;

/// <summary>
/// Review page addresses in the web view. On Windows the reviews folder is a WebView2 virtual host. On Mac Catalyst it is
/// the <see cref="Scheme"/> URL scheme. <see cref="MainPage"/> attaches either one to the web view.
/// </summary>
public sealed class ReviewPages(ReplayLibrary library) : IReviewPages
{
#if MACCATALYST
    public const string Scheme = "simfell-review";
    public const string Host = "reviews";
    private const string Root = Scheme + "://" + Host + "/";
#else
    public const string Host = "reviews.fellowshipsim.local";
    private const string Root = "https://" + Host + "/";
#endif

    public string Directory => library.ReviewsDirectory;

    public string Url(string htmlFile) => Root + Uri.EscapeDataString(htmlFile);

    /// <summary>
    /// Path of the review page at <paramref name="url"/>, or null for an address outside the reviews folder or a missing
    /// file. The path is the address's last segment in <see cref="Directory"/>, so no address reaches past the folder.
    /// </summary>
    public string? File(Uri url)
    {
        if (!url.AbsoluteUri.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
            return null;
        string name = Path.GetFileName(Uri.UnescapeDataString(url.AbsolutePath));
        string path = Path.Combine(Directory, name);
        return name.Length > 0 && System.IO.File.Exists(path) ? path : null;
    }
}
