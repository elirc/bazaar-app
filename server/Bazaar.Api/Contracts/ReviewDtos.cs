using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

/// <summary>A published (approved) review as shown on the storefront.</summary>
public sealed record ReviewDto(
    Guid Id,
    string AuthorName,
    int Rating,
    string? Title,
    string Body,
    bool IsVerifiedPurchase,
    int HelpfulCount,
    DateTimeOffset CreatedAt);

/// <summary>A review with product context and moderation state, for the admin queue.</summary>
public sealed record AdminReviewDto(
    Guid Id,
    Guid ProductId,
    string ProductTitle,
    string ProductSlug,
    string AuthorName,
    int Rating,
    string? Title,
    string Body,
    string Status,
    bool IsVerifiedPurchase,
    int HelpfulCount,
    DateTimeOffset CreatedAt);

public sealed record CreateReviewRequest
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; init; }

    [StringLength(160)]
    public string? Title { get; init; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public string? Body { get; init; }
}

public sealed record ModerateReviewRequest
{
    [Required]
    public string? Status { get; init; }
}
