namespace Bazaar.Domain.Orders;

/// <summary>A shipment against an order, carrying tracking details and the specific line quantities shipped.</summary>
public class Shipment
{
    private readonly List<ShipmentLine> _lines = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Carrier { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTimeOffset ShippedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ShipmentLine> Lines => _lines;

    public ShipmentLine AddLine(ShipmentLine line)
    {
        line.ShipmentId = Id;
        _lines.Add(line);
        return line;
    }
}

/// <summary>A quantity of a specific order line included in a shipment.</summary>
public class ShipmentLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public Guid OrderLineItemId { get; set; }
    public Guid? VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
