using Bazaar.Domain;
using Bazaar.Domain.Orders;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Fulfillment;

public sealed record ShipmentLineCommand(Guid OrderLineItemId, int Quantity);

public sealed record CreateShipmentCommand(
    Guid OrderId, string Carrier, string TrackingNumber, IReadOnlyList<ShipmentLineCommand> Lines);

public enum ShipmentCreateStatus { Ok, OrderNotFound, NotShippable, InvalidLine, OverShipment, NoLines }

public sealed record CreateShipmentOutcome(ShipmentCreateStatus Status, Shipment? Shipment, Order? Order, string? Detail)
{
    public static CreateShipmentOutcome Ok(Shipment shipment, Order order) => new(ShipmentCreateStatus.Ok, shipment, order, null);
    public static CreateShipmentOutcome Fail(ShipmentCreateStatus status, string detail) => new(status, null, null, detail);
}

/// <summary>
/// Creates partial shipments against paid orders, guarding against over-shipping a line, then derives
/// the order status from cumulative shipment coverage (PartiallyFulfilled or Fulfilled).
/// </summary>
public sealed class FulfillmentService
{
    private readonly BazaarDbContext _db;

    public FulfillmentService(BazaarDbContext db) => _db = db;

    public async Task<CreateShipmentOutcome> CreateShipmentAsync(CreateShipmentCommand command, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
        if (order is null)
            return CreateShipmentOutcome.Fail(ShipmentCreateStatus.OrderNotFound, "Order not found.");
        if (order.Status is not (OrderStatus.Paid or OrderStatus.PartiallyFulfilled))
            return CreateShipmentOutcome.Fail(ShipmentCreateStatus.NotShippable, "Only paid orders can be shipped.");

        var requested = command.Lines.Where(l => l.Quantity > 0).ToList();
        if (requested.Count == 0)
            return CreateShipmentOutcome.Fail(ShipmentCreateStatus.NoLines, "A shipment must include at least one line.");

        // Quantity already shipped per order line (across existing shipments).
        var priorLines = await _db.Shipments.Where(s => s.OrderId == order.Id)
            .SelectMany(s => s.Lines)
            .ToListAsync(ct);
        var shipped = priorLines.GroupBy(l => l.OrderLineItemId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var shipment = new Shipment
        {
            OrderId = order.Id,
            Carrier = command.Carrier.Trim(),
            TrackingNumber = command.TrackingNumber.Trim(),
        };

        foreach (var line in requested)
        {
            var orderLine = order.Items.FirstOrDefault(i => i.Id == line.OrderLineItemId);
            if (orderLine is null)
                return CreateShipmentOutcome.Fail(ShipmentCreateStatus.InvalidLine, "A shipment line is not part of this order.");

            var already = shipped.TryGetValue(orderLine.Id, out var q) ? q : 0;
            if (line.Quantity + already > orderLine.Quantity)
                return CreateShipmentOutcome.Fail(ShipmentCreateStatus.OverShipment,
                    $"Cannot ship {line.Quantity} of '{orderLine.Sku}': only {orderLine.Quantity - already} remain unshipped.");

            shipment.AddLine(new ShipmentLine
            {
                OrderLineItemId = orderLine.Id,
                VariantId = orderLine.VariantId,
                Sku = orderLine.Sku,
                Title = orderLine.Title,
                Quantity = line.Quantity,
            });
            shipped[orderLine.Id] = already + line.Quantity;
        }

        _db.Shipments.Add(shipment);

        // Derive status from cumulative coverage.
        var fullyShipped = order.Items.All(li => shipped.GetValueOrDefault(li.Id) >= li.Quantity);
        var anyShipped = order.Items.Any(li => shipped.GetValueOrDefault(li.Id) > 0);
        order.Status = fullyShipped ? OrderStatus.Fulfilled : anyShipped ? OrderStatus.PartiallyFulfilled : order.Status;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return CreateShipmentOutcome.Ok(shipment, order);
    }
}
