using Bazaar.Api.Contracts;
using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class ShippingEndpoints
{
    public static IEndpointRouteBuilder MapShippingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/checkout/shipping-options", ShippingOptions).WithTags("Checkout");
        return app;
    }

    private static async Task<IResult> ShippingOptions(BazaarDbContext db, string? cartToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cartToken))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["cartToken"] = new[] { "A cart token is required." } });

        var cart = await db.Carts.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Variant!)
            .FirstOrDefaultAsync(c => c.Token == cartToken && c.Status == CartStatus.Open, ct);
        if (cart is null) return Results.NotFound();

        var currency = cart.Items.Count > 0 ? cart.Items[0].Variant!.Price.Currency : Money.DefaultCurrency;
        var subtotal = cart.Items.Aggregate(
            Money.Zero(currency),
            (acc, i) => acc.Add(i.Variant!.Price.MultiplyBy(i.Quantity)));
        var itemCount = cart.Items.Sum(i => i.Quantity);
        var weight = cart.Items.Sum(i => i.Variant!.WeightGrams * i.Quantity);

        var methods = await db.ShippingMethods.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);

        var options = methods
            .Select(m => m.ToOptionDto(m.CalculateCost(subtotal, itemCount, weight)))
            .ToList();

        return Results.Ok(options);
    }
}
