using Bazaar.Domain.Catalog;

namespace Bazaar.Domain.Carts;

/// <summary>A guest shopping cart, identified by an opaque token. Aggregate root over its line items.</summary>
public class Cart
{
    /// <summary>Maximum quantity of a single variant permitted in a cart line.</summary>
    public const int MaxQuantityPerLine = 99;

    private readonly List<CartLineItem> _items = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Owning customer when the cart is created (or claimed) by a signed-in account; null for guests.</summary>
    public Guid? CustomerId { get; set; }

    public CartStatus Status { get; set; } = CartStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<CartLineItem> Items => _items;

    public int TotalQuantity => _items.Sum(i => i.Quantity);

    /// <summary>Add a variant, merging with an existing line for the same variant. Enforces quantity rules.</summary>
    public CartLineItem AddItem(ProductVariant variant, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 1.");

        var existing = _items.FirstOrDefault(i => i.VariantId == variant.Id);
        var newQuantity = (existing?.Quantity ?? 0) + quantity;
        if (newQuantity > MaxQuantityPerLine)
            throw new InvalidOperationException(
                $"Quantity {newQuantity} exceeds the per-line maximum of {MaxQuantityPerLine}.");

        if (existing is not null)
        {
            existing.Quantity = newQuantity;
            Touch();
            return existing;
        }

        var item = new CartLineItem
        {
            CartId = Id,
            VariantId = variant.Id,
            Quantity = quantity,
        };
        _items.Add(item);
        Touch();
        return item;
    }

    /// <summary>Set an absolute quantity for a line; a quantity of 0 removes it.</summary>
    public void UpdateQuantity(Guid variantId, int quantity)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        if (quantity > MaxQuantityPerLine)
            throw new InvalidOperationException(
                $"Quantity {quantity} exceeds the per-line maximum of {MaxQuantityPerLine}.");

        var item = _items.FirstOrDefault(i => i.VariantId == variantId)
            ?? throw new InvalidOperationException("Cart does not contain that variant.");

        if (quantity == 0)
            _items.Remove(item);
        else
            item.Quantity = quantity;
        Touch();
    }

    public void RemoveItem(Guid variantId)
    {
        var item = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (item is null) return;
        _items.Remove(item);
        Touch();
    }

    public void Clear()
    {
        _items.Clear();
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
