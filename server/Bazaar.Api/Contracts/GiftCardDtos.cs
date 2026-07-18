using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record GiftCardDto(
    Guid Id,
    string Code,
    MoneyDto Balance,
    MoneyDto InitialBalance,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Public gift-card lookup: never leaks whether an unknown code exists as anything but invalid.</summary>
public sealed record GiftCardBalanceDto(
    string Code,
    bool Valid,
    MoneyDto? Balance);

public sealed record IssueGiftCardRequest
{
    [Required, Range(0.01, 100000)]
    public decimal? Amount { get; init; }

    [StringLength(40, MinimumLength = 4)]
    public string? Code { get; init; }

    [StringLength(3, MinimumLength = 3)]
    public string? Currency { get; init; }
}
