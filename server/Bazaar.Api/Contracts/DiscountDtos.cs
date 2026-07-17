using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record DiscountDto(
    Guid Id,
    string Code,
    string Type,
    decimal Value,
    string Currency,
    bool IsActive,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int? UsageLimit,
    int TimesUsed);

public sealed record DiscountPreviewDto(
    string Code,
    bool Valid,
    string? Reason,
    MoneyDto? Discount);

public sealed record CreateDiscountRequest
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string? Code { get; init; }

    [Required]
    public string? Type { get; init; }

    [Required, Range(0, 1_000_000)]
    public decimal? Value { get; init; }

    [StringLength(3, MinimumLength = 3)]
    public string? Currency { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTimeOffset? StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }

    [Range(1, 1_000_000)]
    public int? UsageLimit { get; init; }
}

public sealed record TransitionOrderRequest
{
    [Required]
    public string? Status { get; init; }
}
