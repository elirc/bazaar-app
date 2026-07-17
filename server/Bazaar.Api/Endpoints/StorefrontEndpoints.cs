using Bazaar.Api.Contracts;
using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class StorefrontEndpoints
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 60;

    public static IEndpointRouteBuilder MapStorefrontEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/storefront").WithTags("Storefront");

        group.MapGet("/products", GetProducts);
        group.MapGet("/products/{slug}", GetProductBySlug);
        group.MapGet("/collections", GetCollections);
        group.MapGet("/discounts/{code}", PreviewDiscount);

        return app;
    }

    private static async Task<IResult> PreviewDiscount(
        BazaarDbContext db, string code, decimal? subtotal, string? currency, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var discount = await db.DiscountCodes.AsNoTracking().FirstOrDefaultAsync(d => d.Code == normalized, ct);
        if (discount is null)
            return Results.Ok(new DiscountPreviewDto(normalized, false, "Unknown discount code.", null));
        if (!discount.IsRedeemable(DateTimeOffset.UtcNow))
            return Results.Ok(new DiscountPreviewDto(normalized, false, "This code is not currently redeemable.", null));

        var cur = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.ToUpperInvariant();
        var amount = subtotal.HasValue
            ? discount.ComputeDiscount(new Money(subtotal.Value, cur))
            : Money.Zero(cur);
        return Results.Ok(new DiscountPreviewDto(normalized, true, null, amount.ToDto()));
    }

    private static async Task<IResult> GetProducts(
        BazaarDbContext db,
        string? search,
        string? collection,
        string? sort,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var (pageNumber, size) = Paging.Clamp(page, pageSize, DefaultPageSize, MaxPageSize);

        var query = db.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Title, $"%{term}%") ||
                (p.Vendor != null && EF.Functions.Like(p.Vendor, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(collection))
        {
            var slug = collection.Trim();
            query = query.Where(p => p.Collections.Any(c => c.Slug == slug));
        }

        query = sort switch
        {
            "price_asc" => query.OrderBy(p => p.Variants.Min(v => v.Price.Amount)).ThenBy(p => p.Title),
            "price_desc" => query.OrderByDescending(p => p.Variants.Min(v => v.Price.Amount)).ThenBy(p => p.Title),
            "title_asc" => query.OrderBy(p => p.Title),
            "title_desc" => query.OrderByDescending(p => p.Title),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Title),
        };

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(p => new ProductSummaryDto(
                p.Id,
                p.Slug,
                p.Title,
                p.Vendor,
                p.Status.ToString(),
                p.Images.OrderBy(i => i.Position).Select(i => i.Url).FirstOrDefault(),
                p.Variants
                    .OrderBy(v => v.Price.Amount)
                    .Select(v => new MoneyDto(v.Price.Amount, v.Price.Currency))
                    .FirstOrDefault(),
                p.Collections.Select(c => c.Slug).ToList()))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<ProductSummaryDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> GetProductBySlug(BazaarDbContext db, string slug, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .Include(p => p.Collections)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == ProductStatus.Active, ct);

        if (product is null)
            return Results.NotFound();

        var variantIds = product.Variants.Select(v => v.Id).ToList();
        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => variantIds.Contains(i.VariantId))
            .Select(i => new { i.VariantId, Available = i.OnHand - i.Reserved })
            .ToListAsync(ct);
        var stockMap = stock.ToDictionary(s => s.VariantId, s => Math.Max(0, s.Available));

        return Results.Ok(product.ToDetailDto(stockMap));
    }

    private static async Task<IResult> GetCollections(BazaarDbContext db, CancellationToken ct)
    {
        var collections = await db.Collections.AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new CollectionDto(
                c.Id,
                c.Slug,
                c.Title,
                c.Description,
                c.Products.Count(p => p.Status == ProductStatus.Active)))
            .ToListAsync(ct);

        return Results.Ok(collections);
    }
}

public static class Paging
{
    public static (int page, int pageSize) Clamp(int? page, int? pageSize, int defaultSize, int maxSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var size = pageSize switch
        {
            null => defaultSize,
            < 1 => defaultSize,
            _ => Math.Min(pageSize.Value, maxSize),
        };
        return (p, size);
    }
}
