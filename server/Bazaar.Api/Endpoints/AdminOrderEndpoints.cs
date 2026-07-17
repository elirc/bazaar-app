using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Orders;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminOrderEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/orders").WithTags("Admin: Orders");
        group.MapGet("/", ListOrders);
        group.MapGet("/{id:guid}", GetOrder);
        group.MapPost("/{id:guid}/transition", TransitionOrder);
        return app;
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
        return order is null ? Results.NotFound() : Results.Ok(order.ToDto());
    }

    private static async Task<IResult> TransitionOrder(
        BazaarDbContext db, Guid id, TransitionOrderRequest request, CancellationToken ct)
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
        return Results.Ok(order.ToDto());
    }
}
