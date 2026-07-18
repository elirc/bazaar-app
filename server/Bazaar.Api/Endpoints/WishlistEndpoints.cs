using System.Security.Claims;
using Bazaar.Api.Auth;
using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Carts;
using Bazaar.Domain.Wishlists;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class WishlistEndpoints
{
    public static IEndpointRouteBuilder MapWishlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").WithTags("Wishlists").RequireAuthorization();
        group.MapGet("/wishlists", ListWishlists);
        group.MapPost("/wishlists", CreateWishlist);
        group.MapDelete("/wishlists/{id:guid}", DeleteWishlist);
        group.MapPost("/wishlist/items", AddToDefault);
        group.MapPost("/wishlists/{id:guid}/items", AddToWishlist);
        group.MapDelete("/wishlists/{id:guid}/items/{variantId:guid}", RemoveItem);
        group.MapPost("/wishlists/{id:guid}/items/{variantId:guid}/move-to-cart", MoveToCart);
        return app;
    }

    private static async Task<IResult> ListWishlists(BazaarDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        await EnsureDefault(db, customerId.Value, ct);

        var wishlists = await db.Wishlists
            .Include(w => w.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .Where(w => w.CustomerId == customerId)
            .AsNoTracking()
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
            .ToListAsync(ct);

        var availability = await AvailabilityFor(db, wishlists.SelectMany(w => w.Items.Select(i => i.VariantId)).ToList(), ct);
        return Results.Ok(wishlists.Select(w => ToDto(w, availability)).ToList());
    }

    private static async Task<IResult> CreateWishlist(
        BazaarDbContext db, ClaimsPrincipal principal, CreateWishlistRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var wishlist = new Wishlist { CustomerId = customerId.Value, Name = request.Name!.Trim(), IsDefault = false };
        db.Wishlists.Add(wishlist);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/account/wishlists/{wishlist.Id}",
            ToDto(wishlist, new Dictionary<Guid, int>()));
    }

    private static async Task<IResult> DeleteWishlist(BazaarDbContext db, ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var wishlist = await db.Wishlists.FirstOrDefaultAsync(w => w.Id == id && w.CustomerId == customerId, ct);
        if (wishlist is null) return Results.NotFound();
        if (wishlist.IsDefault)
            return Results.Problem("The default wishlist cannot be deleted.", statusCode: StatusCodes.Status400BadRequest, title: "Cannot delete default");

        db.Wishlists.Remove(wishlist);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddToDefault(
        BazaarDbContext db, ClaimsPrincipal principal, AddWishlistItemRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();
        var wishlist = await EnsureDefault(db, customerId.Value, ct);
        return await AddItemTo(db, wishlist, request, ct);
    }

    private static async Task<IResult> AddToWishlist(
        BazaarDbContext db, ClaimsPrincipal principal, Guid id, AddWishlistItemRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();
        var wishlist = await db.Wishlists.Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id && w.CustomerId == customerId, ct);
        if (wishlist is null) return Results.NotFound();
        return await AddItemTo(db, wishlist, request, ct);
    }

    private static async Task<IResult> AddItemTo(
        BazaarDbContext db, Wishlist wishlist, AddWishlistItemRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var variantId = request.VariantId!.Value;
        if (!await db.Variants.AnyAsync(v => v.Id == variantId, ct))
            return Results.Problem("That product variant does not exist.", statusCode: StatusCodes.Status404NotFound, title: "Variant not found");

        var available = await AvailableFor(db, variantId, ct);
        wishlist.AddItem(variantId, outOfStockWhenAdded: available <= 0);
        await db.SaveChangesAsync(ct);

        return Results.Ok(await ReloadWishlist(db, wishlist.Id, ct));
    }

    private static async Task<IResult> RemoveItem(
        BazaarDbContext db, ClaimsPrincipal principal, Guid id, Guid variantId, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var wishlist = await db.Wishlists.Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id && w.CustomerId == customerId, ct);
        if (wishlist is null) return Results.NotFound();

        wishlist.RemoveItem(variantId);
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ReloadWishlist(db, wishlist.Id, ct));
    }

    private static async Task<IResult> MoveToCart(
        BazaarDbContext db, ClaimsPrincipal principal, Guid id, Guid variantId, MoveToCartRequest request, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var wishlist = await db.Wishlists.Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id && w.CustomerId == customerId, ct);
        if (wishlist is null || wishlist.Items.All(i => i.VariantId != variantId))
            return Results.NotFound();

        var variant = await db.Variants.Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == variantId, ct);
        if (variant is null || variant.Product is null || variant.Product.Status != ProductStatus.Active)
            return Results.Problem("That product is not available.", statusCode: StatusCodes.Status409Conflict, title: "Variant not available");

        Cart? cart = null;
        if (!string.IsNullOrWhiteSpace(request.CartToken))
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Token == request.CartToken && c.Status == CartStatus.Open, ct);
        if (cart is null)
        {
            cart = new Cart { CustomerId = customerId };
            db.Carts.Add(cart);
        }

        cart.AddItem(variant, 1);
        wishlist.RemoveItem(variantId);
        await db.SaveChangesAsync(ct);

        return Results.Ok(await LoadCartDto(db, cart.Token, ct));
    }

    // ---- Helpers ----

    private static async Task<Wishlist> EnsureDefault(BazaarDbContext db, Guid customerId, CancellationToken ct)
    {
        var existing = await db.Wishlists.Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.IsDefault, ct);
        if (existing is not null) return existing;

        var wishlist = new Wishlist { CustomerId = customerId, Name = "My Wishlist", IsDefault = true };
        db.Wishlists.Add(wishlist);
        await db.SaveChangesAsync(ct);
        return wishlist;
    }

    private static async Task<WishlistDto> ReloadWishlist(BazaarDbContext db, Guid wishlistId, CancellationToken ct)
    {
        var wishlist = await db.Wishlists.AsNoTracking()
            .Include(w => w.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .FirstAsync(w => w.Id == wishlistId, ct);
        var availability = await AvailabilityFor(db, wishlist.Items.Select(i => i.VariantId).ToList(), ct);
        return ToDto(wishlist, availability);
    }

    private static WishlistDto ToDto(Wishlist wishlist, IReadOnlyDictionary<Guid, int> availability) => new(
        wishlist.Id,
        wishlist.Name,
        wishlist.IsDefault,
        wishlist.Items
            .OrderByDescending(i => i.CreatedAt)
            .Select(i =>
            {
                var variant = i.Variant;
                var available = availability.TryGetValue(i.VariantId, out var qty) ? qty : 0;
                return new WishlistItemDto(
                    i.VariantId,
                    variant?.Product?.Slug ?? string.Empty,
                    variant?.Product?.Title ?? variant?.Title ?? string.Empty,
                    variant?.Title ?? string.Empty,
                    variant?.Sku ?? string.Empty,
                    (variant?.Price ?? Bazaar.Domain.Common.Money.Zero()).ToDto(),
                    available,
                    i.OutOfStockWhenAdded && available > 0,
                    i.CreatedAt);
            })
            .ToList());

    private static async Task<Dictionary<Guid, int>> AvailabilityFor(BazaarDbContext db, List<Guid> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0) return new Dictionary<Guid, int>();
        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => variantIds.Contains(i.VariantId))
            .Select(i => new { i.VariantId, Available = i.OnHand - i.Reserved })
            .ToListAsync(ct);
        return stock.ToDictionary(s => s.VariantId, s => Math.Max(0, s.Available));
    }

    private static async Task<int> AvailableFor(BazaarDbContext db, Guid variantId, CancellationToken ct)
    {
        var item = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.VariantId == variantId, ct);
        return item is null ? 0 : Math.Max(0, item.OnHand - item.Reserved);
    }

    private static async Task<CartDto?> LoadCartDto(BazaarDbContext db, string token, CancellationToken ct)
    {
        var cart = await db.Carts.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Token == token, ct);
        if (cart is null) return null;

        var availability = await AvailabilityFor(db, cart.Items.Select(i => i.VariantId).ToList(), ct);
        return cart.ToDto(availability);
    }
}
