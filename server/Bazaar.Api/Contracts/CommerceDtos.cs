using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

// ---- Cart ----

public sealed record CartLineDto(
    Guid VariantId,
    string ProductSlug,
    string ProductTitle,
    string VariantTitle,
    string Sku,
    MoneyDto UnitPrice,
    int Quantity,
    MoneyDto LineTotal,
    int Available);

public sealed record CartDto(
    Guid Id,
    string Token,
    IReadOnlyList<CartLineDto> Items,
    MoneyDto Subtotal,
    int ItemCount);

public sealed record AddCartItemRequest
{
    // Nullable so a missing value fails validation (a non-nullable Guid would bind to Guid.Empty).
    [Required]
    public Guid? VariantId { get; init; }

    [Range(1, 99)]
    public int Quantity { get; init; } = 1;
}

public sealed record UpdateCartItemRequest
{
    [Range(0, 99)]
    public int Quantity { get; init; }
}

// ---- Orders / checkout ----

public sealed record AddressDto(
    string Name,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country);

public sealed record OrderLineDto(
    string Sku,
    string Title,
    int Quantity,
    MoneyDto UnitPrice,
    MoneyDto LineTotal);

public sealed record OrderSummaryDto(
    Guid Id,
    string Number,
    string Email,
    string Status,
    MoneyDto GrandTotal,
    int ItemCount,
    DateTimeOffset PlacedAt);

public sealed record OrderDto(
    Guid Id,
    string Number,
    string Email,
    string Status,
    string Currency,
    AddressDto ShippingAddress,
    MoneyDto Subtotal,
    MoneyDto DiscountTotal,
    MoneyDto TaxTotal,
    MoneyDto ShippingTotal,
    MoneyDto GrandTotal,
    string? DiscountCode,
    IReadOnlyList<OrderLineDto> Items,
    DateTimeOffset PlacedAt);

public sealed record AddressInput
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string? Name { get; init; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string? Line1 { get; init; }

    [StringLength(200)]
    public string? Line2 { get; init; }

    [Required, StringLength(120, MinimumLength = 1)]
    public string? City { get; init; }

    [StringLength(120)]
    public string? Region { get; init; }

    [Required, StringLength(20, MinimumLength = 1)]
    public string? PostalCode { get; init; }

    [Required, StringLength(2, MinimumLength = 2, ErrorMessage = "Country must be a 2-letter code.")]
    public string? Country { get; init; }
}

public sealed record CheckoutRequest
{
    [Required]
    public string? CartToken { get; init; }

    [Required, EmailAddress]
    public string? Email { get; init; }

    [Required]
    public AddressInput? ShippingAddress { get; init; }

    public string? DiscountCode { get; init; }
}
