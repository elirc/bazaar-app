using Bazaar.Domain.Common;

namespace Bazaar.Domain.Orders;

/// <summary>A placed order. Aggregate root over its line items; owns its money totals and shipping address.</summary>
public class Order
{
    private readonly List<OrderLineItem> _items = new();

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Human-friendly sequential-ish order number, e.g. "BZ-1001".</summary>
    public string Number { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Currency { get; set; } = Money.DefaultCurrency;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public Address ShippingAddress { get; set; } = null!;

    public Money Subtotal { get; set; } = Money.Zero();
    public Money DiscountTotal { get; set; } = Money.Zero();
    public Money TaxTotal { get; set; } = Money.Zero();
    public Money ShippingTotal { get; set; } = Money.Zero();
    public Money GrandTotal { get; set; } = Money.Zero();

    public string? DiscountCode { get; set; }

    /// <summary>Display name of the chosen shipping method (e.g. "Standard").</summary>
    public string? ShippingMethod { get; set; }

    /// <summary>Gift-card tender applied to this order (the remainder is charged to the payment gateway).</summary>
    public Money GiftCardTotal { get; set; } = Money.Zero();
    public string? GiftCardCode { get; set; }

    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<OrderLineItem> Items => _items;

    public OrderLineItem AddItem(OrderLineItem item)
    {
        item.OrderId = Id;
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Valid MANUAL lifecycle transitions. Fulfillment (Paid -> PartiallyFulfilled/Fulfilled) is
    /// driven by shipment coverage, not by this table. Cancellation is blocked once anything ships.
    /// </summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.Pending => to is OrderStatus.Paid or OrderStatus.Cancelled,
        OrderStatus.Paid => to is OrderStatus.Refunded or OrderStatus.Cancelled,
        OrderStatus.PartiallyFulfilled => to is OrderStatus.Refunded,
        OrderStatus.Fulfilled => to is OrderStatus.Refunded,
        _ => false,
    };

    public void TransitionTo(OrderStatus target)
    {
        if (Status == target) return;
        if (!CanTransition(Status, target))
            throw new InvalidOperationException($"Cannot transition order from {Status} to {target}.");
        Status = target;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
