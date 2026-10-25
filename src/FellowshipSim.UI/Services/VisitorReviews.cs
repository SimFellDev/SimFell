using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The reviews each visitor ran. The review files are in the <see cref="ReplayLibrary"/>'s reviews folder, and a web
/// visitor's review is theirs once their <c>reviews/&lt;review id&gt;</c> is in the <see cref="IBlobStore"/>. The data
/// folder's owner has every review in the folder.
/// </summary>
public sealed class VisitorReviews(ReplayLibrary library, IBlobStore store)
{
    public Task AddAsync(Visitor visitor, ReviewSummary review, CancellationToken ct = default) =>
        visitor.OwnsDataFolder ? Task.CompletedTask : store.WriteAsync(KeyOf(visitor, review.Id), review.Id, ct);

    /// <summary>The visitor's reviews whose page is in the reviews folder, newest first.</summary>
    public async Task<IReadOnlyList<ReviewSummary>> ListAsync(Visitor visitor, CancellationToken ct = default)
    {
        if (visitor.OwnsDataFolder) return library.ListReviews();
        return (await store.ListAsync(visitor.Key("reviews"), ct))
            .Select(key => library.GetReview(key[(key.LastIndexOf('/') + 1)..]))
            .OfType<ReviewSummary>()
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    /// <summary>True when the review <paramref name="reviewId"/> is the visitor's.</summary>
    public async Task<bool> HasAsync(Visitor visitor, string reviewId, CancellationToken ct = default) =>
        visitor.OwnsDataFolder || DiskBlobStore.IsSegment(reviewId) && await store.ReadAsync(KeyOf(visitor, reviewId), ct) != null;

    private static string KeyOf(Visitor visitor, string reviewId) => visitor.Key($"reviews/{reviewId}");
}
