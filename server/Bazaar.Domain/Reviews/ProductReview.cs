namespace Bazaar.Domain.Reviews;

/// <summary>
/// A customer's rating and written review of a product. One per customer per product. Reviews are
/// moderated (start <see cref="ReviewStatus.Pending"/>) and only <see cref="ReviewStatus.Approved"/>
/// reviews count toward the product's aggregate rating and are shown on the storefront.
/// </summary>
public class ProductReview
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Snapshot of the reviewer's display name at submission time.</summary>
    public string AuthorName { get; set; } = string.Empty;

    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public bool IsVerifiedPurchase { get; set; }
    public int HelpfulCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Moderate(ReviewStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
