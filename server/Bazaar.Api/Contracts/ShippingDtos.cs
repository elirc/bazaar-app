using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

/// <summary>A shipping method with its cost computed for a specific cart.</summary>
public sealed record ShippingOptionDto(
    string Code,
    string Name,
    string RateType,
    MoneyDto Cost,
    string DeliveryEstimate,
    int MinDays,
    int MaxDays);

// ---- Address book ----

public sealed record CustomerAddressDto(
    Guid Id,
    string? Label,
    bool IsDefault,
    AddressDto Address);

public sealed record UpsertAddressRequest
{
    [StringLength(60)]
    public string? Label { get; init; }

    public bool IsDefault { get; init; }

    [Required]
    public AddressInput? Address { get; init; }
}
