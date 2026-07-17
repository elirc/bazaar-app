using System.Security.Claims;
using Bazaar.Api.Auth;
using Bazaar.Api.Contracts;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

/// <summary>The signed-in customer's own resources (order history). Scoped to their account only.</summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").WithTags("Account").RequireAuthorization();
        group.MapGet("/orders", ListOrders);
        group.MapGet("/orders/{id:guid}", GetOrder);
        return app;
    }

    private static async Task<IResult> ListOrders(BazaarDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .Select(o => new OrderSummaryDto(
                o.Id, o.Number, o.Email, o.Status.ToString(),
                new MoneyDto(o.GrandTotal.Amount, o.GrandTotal.Currency),
                o.Items.Sum(li => li.Quantity),
                o.PlacedAt))
            .ToListAsync(ct);

        return Results.Ok(orders);
    }

    private static async Task<IResult> GetOrder(BazaarDbContext db, ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        // A customer may only read their own order; anything else is a 404 (no existence leak).
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == customerId, ct);

        return order is null ? Results.NotFound() : Results.Ok(order.ToDto());
    }
}
