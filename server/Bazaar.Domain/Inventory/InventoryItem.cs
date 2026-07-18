using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;

namespace Bazaar.Domain.Inventory;

/// <summary>
/// Stock record for a single variant. <see cref="Available"/> is what a shopper can buy:
/// on-hand minus the quantity currently reserved by in-flight checkouts.
/// </summary>
public class InventoryItem : IConcurrencyStamped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid VariantId { get; set; }
    public int OnHand { get; set; }
    public int Reserved { get; set; }

    /// <summary>Optimistic-concurrency token, refreshed on every stock update.</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public ProductVariant? Variant { get; set; }

    public int Available => Math.Max(0, OnHand - Reserved);

    public bool CanReserve(int quantity) => quantity > 0 && Available >= quantity;

    /// <summary>Reserve stock for an in-flight checkout. Throws if insufficient.</summary>
    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (!CanReserve(quantity))
            throw new InvalidOperationException(
                $"Insufficient stock to reserve {quantity} (available {Available}).");
        Reserved += quantity;
    }

    /// <summary>Release a previous reservation (e.g. on cancellation or expiry).</summary>
    public void Release(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        Reserved = Math.Max(0, Reserved - quantity);
    }

    /// <summary>Commit a reservation on payment: reduce both reserved and on-hand.</summary>
    public void Commit(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        Reserved = Math.Max(0, Reserved - quantity);
        OnHand = Math.Max(0, OnHand - quantity);
    }
}
