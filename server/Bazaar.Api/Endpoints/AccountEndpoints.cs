using System.Security.Claims;
using Bazaar.Api.Auth;
using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain.Customers;
using Bazaar.Infrastructure.Persistence;
using Bazaar.Infrastructure.Returns;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

/// <summary>The signed-in customer's own resources (order history, address book). Scoped to their account only.</summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").WithTags("Account").RequireAuthorization();
        group.MapGet("/orders", ListOrders);
        group.MapGet("/orders/{id:guid}", GetOrder);
        group.MapGet("/addresses", ListAddresses);
        group.MapPost("/addresses", CreateAddress);
        group.MapPut("/addresses/{id:guid}", UpdateAddress);
        group.MapDelete("/addresses/{id:guid}", DeleteAddress);
        group.MapGet("/returns", ListReturns);
        group.MapPost("/orders/{orderId:guid}/returns", CreateReturn);
        return app;
    }

    // ---- Returns ----

    private static async Task<IResult> ListReturns(BazaarDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var returns = await db.ReturnRequests.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var numbers = await OrderNumbers(db, returns.Select(r => r.OrderId), ct);
        return Results.Ok(returns.Select(r => r.ToDto(numbers.GetValueOrDefault(r.OrderId, string.Empty))).ToList());
    }

    private static async Task<IResult> CreateReturn(
        BazaarDbContext db, ReturnService returns, ClaimsPrincipal principal, Guid orderId, CreateReturnRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var command = new CreateReturnCommand(
            orderId, customerId, request.Reason,
            request.Lines.Select(l => new ReturnLineCommand(l.OrderLineItemId!.Value, l.Quantity)).ToList());
        var outcome = await returns.CreateAsync(command, ct);

        if (outcome.Status != ReturnCreateStatus.Ok)
            return outcome.Status switch
            {
                ReturnCreateStatus.OrderNotFound => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status404NotFound, title: "Order not found"),
                ReturnCreateStatus.NotFulfilled => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Order not returnable"),
                ReturnCreateStatus.OverRefund => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Over-refund"),
                _ => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid return"),
            };

        var number = (await OrderNumbers(db, new[] { orderId }, ct)).GetValueOrDefault(orderId, string.Empty);
        return Results.Created($"/api/account/returns", outcome.Return!.ToDto(number));
    }

    private static async Task<Dictionary<Guid, string>> OrderNumbers(BazaarDbContext db, IEnumerable<Guid> orderIds, CancellationToken ct)
    {
        var ids = orderIds.Distinct().ToList();
        return await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.Number })
            .ToDictionaryAsync(o => o.Id, o => o.Number, ct);
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
        if (order is null) return Results.NotFound();

        var shipments = await AdminOrderEndpoints.LoadShipments(db, id, ct);
        return Results.Ok(order.ToDto(shipments));
    }

    // ---- Address book ----

    private static async Task<IResult> ListAddresses(BazaarDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var addresses = await db.CustomerAddresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(addresses.Select(a => a.ToDto()).ToList());
    }

    private static async Task<IResult> CreateAddress(
        BazaarDbContext db, ClaimsPrincipal principal, UpsertAddressRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var children = request.Address is null
            ? Enumerable.Empty<(string, object)>()
            : new[] { ("address", (object)request.Address) };
        if (!RequestValidation.TryValidateGraph(request, children, out var errors))
            return Results.ValidationProblem(errors);

        var isDefault = request.IsDefault || !await db.CustomerAddresses.AnyAsync(a => a.CustomerId == customerId, ct);
        if (isDefault)
            await ClearDefaults(db, customerId.Value, ct);

        var address = new CustomerAddress
        {
            CustomerId = customerId.Value,
            Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label!.Trim(),
            IsDefault = isDefault,
            Address = request.Address!.ToAddress(),
        };
        db.CustomerAddresses.Add(address);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/account/addresses/{address.Id}", address.ToDto());
    }

    private static async Task<IResult> UpdateAddress(
        BazaarDbContext db, ClaimsPrincipal principal, Guid id, UpsertAddressRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var children = request.Address is null
            ? Enumerable.Empty<(string, object)>()
            : new[] { ("address", (object)request.Address) };
        if (!RequestValidation.TryValidateGraph(request, children, out var errors))
            return Results.ValidationProblem(errors);

        var address = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, ct);
        if (address is null) return Results.NotFound();

        if (request.IsDefault && !address.IsDefault)
            await ClearDefaults(db, customerId.Value, ct);

        address.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label!.Trim();
        address.IsDefault = request.IsDefault || address.IsDefault;
        address.Address = request.Address!.ToAddress();
        await db.SaveChangesAsync(ct);

        return Results.Ok(address.ToDto());
    }

    private static async Task<IResult> DeleteAddress(
        BazaarDbContext db, ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var address = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, ct);
        if (address is null) return Results.NotFound();

        db.CustomerAddresses.Remove(address);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task ClearDefaults(BazaarDbContext db, Guid customerId, CancellationToken ct)
    {
        var current = await db.CustomerAddresses.Where(a => a.CustomerId == customerId && a.IsDefault).ToListAsync(ct);
        foreach (var existing in current)
            existing.IsDefault = false;
    }
}
