using Bazaar.Domain.Common;

namespace Bazaar.Domain.Orders;

/// <summary>A line on an order. Captures a snapshot of the variant (sku/title/price) at purchase time.</summary>
public class OrderLineItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid? VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; } = Money.Zero();
    public Money LineTotal { get; set; } = Money.Zero();
}
