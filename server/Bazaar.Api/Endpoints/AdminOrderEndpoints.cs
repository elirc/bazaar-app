using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Webhooks;
using Bazaar.Infrastructure.Fulfillment;
using Bazaar.Infrastructure.Persistence;
using Bazaar.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminOrderEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/orders").WithTags("Admin: Orders").RequireAuthorization("Admin");
        group.MapGet("/", ListOrders);
        group.MapGet("/{id:guid}", GetOrder);
        group.MapPost("/{id:guid}/transition", TransitionOrder);
        group.MapPost("/{id:guid}/shipments", CreateShipment);
        return app;
    }

    internal static Task<List<Shipment>> LoadShipments(BazaarDbContext db, Guid orderId, CancellationToken ct) =>
        db.Shipments.AsNoTracking().Include(s => s.Lines).Where(s => s.OrderId == orderId).ToListAsync(ct);

    private static async Task<IResult> CreateShipment(
        BazaarDbContext db, FulfillmentService fulfillment, WebhookDispatcher webhooks,
        Guid id, CreateShipmentRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var command = new CreateShipmentCommand(
            id, request.Carrier!, request.TrackingNumber!,
            request.Lines.Select(l => new ShipmentLineCommand(l.OrderLineItemId!.Value, l.Quantity)).ToList());
        var outcome = await fulfillment.CreateShipmentAsync(command, ct);

        if (outcome.Status != ShipmentCreateStatus.Ok)
            return outcome.Status switch
            {
                ShipmentCreateStatus.OrderNotFound => Results.NotFound(),
                ShipmentCreateStatus.NotShippable => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Order not shippable"),
                ShipmentCreateStatus.OverShipment => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Over-shipment"),
                _ => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid shipment"),
            };

        // Fire order.fulfilled only when this shipment completed full coverage.
        if (outcome.Order!.Status == OrderStatus.Fulfilled)
            await webhooks.DispatchAsync(WebhookEvents.OrderFulfilled, WebhookPayloads.ForOrder(outcome.Order), ct);

        var shipments = await LoadShipments(db, id, ct);
        return Results.Created($"/api/admin/orders/{id}", outcome.Order.ToDto(shipments));
    }

    private static async Task<IResult> ListOrders(
        BazaarDbContext db, string? search, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNumber, size) = Paging.Clamp(page, pageSize, DefaultPageSize, MaxPageSize);
        var query = db.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                EF.Functions.Like(o.Number, $"%{term}%") || EF.Functions.Like(o.Email, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsed))
            query = query.Where(o => o.Status == parsed);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(o => new OrderSummaryDto(
                o.Id, o.Number, o.Email, o.Status.ToString(),
                new MoneyDto(o.GrandTotal.Amount, o.GrandTotal.Currency),
                o.Items.Sum(li => li.Quantity),
                o.PlacedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<OrderSummaryDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> GetOrder(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return Results.NotFound();
        var shipments = await LoadShipments(db, id, ct);
        return Results.Ok(order.ToDto(shipments));
    }

    private static async Task<IResult> TransitionOrder(
        BazaarDbContext db, WebhookDispatcher webhooks, Guid id, TransitionOrderRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var target) || !Enum.IsDefined(target))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = new[] { "Unknown order status." } });

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return Results.NotFound();

        if (!Order.CanTransition(order.Status, target))
            return Results.Problem(
                $"An order cannot move from {order.Status} to {target}.",
                statusCode: StatusCodes.Status409Conflict, title: "Invalid transition");

        var shouldRestock = target is OrderStatus.Cancelled or OrderStatus.Refunded;
        order.TransitionTo(target);

        if (shouldRestock)
        {
            var variantIds = order.Items.Where(i => i.VariantId.HasValue).Select(i => i.VariantId!.Value).ToList();
            var inventory = await db.InventoryItems.Where(i => variantIds.Contains(i.VariantId)).ToListAsync(ct);
            var byVariant = inventory.ToDictionary(i => i.VariantId);
            foreach (var line in order.Items.Where(i => i.VariantId.HasValue))
            {
                if (byVariant.TryGetValue(line.VariantId!.Value, out var stock))
                    stock.OnHand += line.Quantity;
            }
        }

        await db.SaveChangesAsync(ct);

        if (target == OrderStatus.Refunded)
            await webhooks.DispatchAsync(WebhookEvents.OrderRefunded, WebhookPayloads.ForOrder(order), ct);

        return Results.Ok(order.ToDto());
    }
}
