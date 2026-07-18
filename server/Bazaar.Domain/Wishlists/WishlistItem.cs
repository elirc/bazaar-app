using Bazaar.Domain.Catalog;

namespace Bazaar.Domain.Wishlists;

/// <summary>A saved variant on a wishlist. Records whether it was out of stock when added, to power a back-in-stock flag.</summary>
public class WishlistItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WishlistId { get; set; }
    public Guid VariantId { get; set; }

    /// <summary>Snapshot: was the variant unavailable at the moment it was added? Used to flag "back in stock".</summary>
    public bool OutOfStockWhenAdded { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProductVariant? Variant { get; set; }
}
