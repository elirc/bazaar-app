using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Infrastructure.Checkout;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/checkout", Checkout).WithTags("Checkout");
        app.MapGet("/api/orders/{id:guid}", GetOrder).WithTags("Orders");
        return app;
    }

    private static async Task<IResult> Checkout(CheckoutService checkout, CheckoutRequest request, CancellationToken ct)
    {
        var children = request.ShippingAddress is null
            ? Enumerable.Empty<(string, object)>()
            : new[] { ("shippingAddress", (object)request.ShippingAddress) };

        if (!RequestValidation.TryValidateGraph(request, children, out var errors))
            return Results.ValidationProblem(errors);

        var command = new CheckoutCommand(request.CartToken!, request.Email!, request.ShippingAddress!.ToAddress(), request.DiscountCode);
        var outcome = await checkout.CheckoutAsync(command, ct);

        return outcome.Status switch
        {
            CheckoutStatus.Ok => Results.Created($"/api/orders/{outcome.Order!.Id}", outcome.Order.ToDto()),
            CheckoutStatus.CartNotFound => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status404NotFound, title: "Cart not found"),
            CheckoutStatus.CartEmpty => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Cart empty"),
            CheckoutStatus.InsufficientStock => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Insufficient stock"),
            CheckoutStatus.InvalidDiscount => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid discount"),
            CheckoutStatus.PaymentDeclined => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status402PaymentRequired, title: "Payment declined"),
            _ => Results.Problem("Checkout failed."),
        };
    }

    private static async Task<IResult> GetOrder(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? Results.NotFound() : Results.Ok(order.ToDto());
    }
}
