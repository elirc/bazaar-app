namespace Bazaar.Domain.Wishlists;

/// <summary>A customer's wishlist. Each customer has one default list plus any number of named lists.</summary>
public class Wishlist
{
    private readonly List<WishlistItem> _items = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = "My Wishlist";
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<WishlistItem> Items => _items;

    public WishlistItem AddItem(Guid variantId, bool outOfStockWhenAdded)
    {
        var existing = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (existing is not null) return existing;

        var item = new WishlistItem
        {
            WishlistId = Id,
            VariantId = variantId,
            OutOfStockWhenAdded = outOfStockWhenAdded,
        };
        _items.Add(item);
        return item;
    }

    public bool RemoveItem(Guid variantId)
    {
        var item = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (item is null) return false;
        _items.Remove(item);
        return true;
    }
}
