using FellowshipSim.Logs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>Reviews each visitor ran: a web visitor has only their own; the data folder's owner has every review in the folder.</summary>
public sealed class VisitorReviewsTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"simfell-reviews-{Guid.NewGuid():N}");
    private readonly ReplayLibrary _library;
    private readonly VisitorReviews _reviews;
    private static readonly Visitor A = new("a1b2", OwnsDataFolder: false);
    private static readonly Visitor B = new("c3d4", OwnsDataFolder: false);

    public VisitorReviewsTests()
    {
        _library = new ReplayLibrary(_data);
        _reviews = new VisitorReviews(_library, new DiskBlobStore(_data));
    }

    private ReviewSummary Save(string reportCode)
    {
        var summary = new ReviewSummary(ReplayLibrary.ReviewId(reportCode, 1, 2), reportCode, 1, "Fight", null, 2, "Player", "Rime",
            1, 1, 1, 1, 100, 100, 0, DateTime.UtcNow);
        _library.Save(new ReviewArtifacts(summary, "{}"u8.ToArray(), "<html></html>", "", "", "{}"));
        return summary;
    }

    [Fact]
    public async Task A_Visitor_Has_Only_The_Reviews_They_Ran()
    {
        var mine = Save("aaaa");
        Save("bbbb");
        await _reviews.AddAsync(A, mine);

        Assert.Equal([mine.Id], (await _reviews.ListAsync(A)).Select(r => r.Id));
        Assert.True(await _reviews.HasAsync(A, mine.Id));
        Assert.Empty(await _reviews.ListAsync(B));
        Assert.False(await _reviews.HasAsync(B, mine.Id));
        Assert.False(await _reviews.HasAsync(A, "../aaaa_fight1_p2"));
    }

    [Fact]
    public async Task The_Data_Folders_Owner_Has_Every_Review()
    {
        Save("aaaa");
        Save("bbbb");

        Assert.Equal(2, (await _reviews.ListAsync(Visitor.DataFolderOwner)).Count);
        Assert.True(await _reviews.HasAsync(Visitor.DataFolderOwner, ReplayLibrary.ReviewId("bbbb", 1, 2)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_data)) Directory.Delete(_data, recursive: true);
    }
}
