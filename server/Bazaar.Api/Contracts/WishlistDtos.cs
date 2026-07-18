using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record WishlistItemDto(
    Guid VariantId,
    string ProductSlug,
    string ProductTitle,
    string VariantTitle,
    string Sku,
    MoneyDto Price,
    int Available,
    bool BackInStock,
    DateTimeOffset AddedAt);

public sealed record WishlistDto(
    Guid Id,
    string Name,
    bool IsDefault,
    IReadOnlyList<WishlistItemDto> Items);

public sealed record CreateWishlistRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string? Name { get; init; }
}

public sealed record AddWishlistItemRequest
{
    [Required]
    public Guid? VariantId { get; init; }
}

public sealed record MoveToCartRequest
{
    /// <summary>Existing open cart to move the item into; when omitted a new cart is created.</summary>
    public string? CartToken { get; init; }
}
