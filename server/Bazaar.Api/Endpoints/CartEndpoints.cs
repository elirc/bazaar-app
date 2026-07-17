using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Carts;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cart").WithTags("Cart");

        group.MapPost("/", CreateCart);
        group.MapGet("/{token}", GetCart);
        group.MapPost("/{token}/items", AddItem);
        group.MapPut("/{token}/items/{variantId:guid}", UpdateItem);
        group.MapDelete("/{token}/items/{variantId:guid}", RemoveItem);

        return app;
    }

    private static async Task<IResult> CreateCart(BazaarDbContext db, CancellationToken ct)
    {
        var cart = new Cart();
        db.Carts.Add(cart);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/cart/{cart.Token}", cart.ToDto(EmptyAvailability));
    }

    private static async Task<IResult> GetCart(BazaarDbContext db, string token, CancellationToken ct)
    {
        var dto = await LoadCartDto(db, token, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> AddItem(
        BazaarDbContext db, string token, AddCartItemRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var cart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Token == token && c.Status == CartStatus.Open, ct);
        if (cart is null) return Results.NotFound();

        var variant = await db.Variants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, ct);
        if (variant is null || variant.Product is null || variant.Product.Status != ProductStatus.Active)
            return Results.Problem("That product is not available.", statusCode: StatusCodes.Status404NotFound, title: "Variant not available");

        try
        {
            cart.AddItem(variant, request.Quantity);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Quantity not allowed");
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await LoadCartDto(db, token, ct));
    }

    private static async Task<IResult> UpdateItem(
        BazaarDbContext db, string token, Guid variantId, UpdateCartItemRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var cart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Token == token && c.Status == CartStatus.Open, ct);
        if (cart is null) return Results.NotFound();
        if (cart.Items.All(i => i.VariantId != variantId))
            return Results.NotFound();

        cart.UpdateQuantity(variantId, request.Quantity);
        await db.SaveChangesAsync(ct);
        return Results.Ok(await LoadCartDto(db, token, ct));
    }

    private static async Task<IResult> RemoveItem(
        BazaarDbContext db, string token, Guid variantId, CancellationToken ct)
    {
        var cart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Token == token && c.Status == CartStatus.Open, ct);
        if (cart is null) return Results.NotFound();

        cart.RemoveItem(variantId);
        await db.SaveChangesAsync(ct);
        return Results.Ok(await LoadCartDto(db, token, ct));
    }

    private static readonly Dictionary<Guid, int> EmptyAvailability = new();

    private static async Task<CartDto?> LoadCartDto(BazaarDbContext db, string token, CancellationToken ct)
    {
        var cart = await db.Carts.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Token == token, ct);
        if (cart is null) return null;

        var variantIds = cart.Items.Select(i => i.VariantId).ToList();
        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => variantIds.Contains(i.VariantId))
            .Select(i => new { i.VariantId, Available = i.OnHand - i.Reserved })
            .ToListAsync(ct);
        var availability = stock.ToDictionary(s => s.VariantId, s => Math.Max(0, s.Available));

        return cart.ToDto(availability);
    }
}
