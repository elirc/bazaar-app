using Bazaar.Api.Contracts;
using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminReportEndpoints
{
    // Statuses that represent a real sale (paid or beyond; excludes Pending and Cancelled).
    private static readonly OrderStatus[] SoldStatuses =
        { OrderStatus.Paid, OrderStatus.PartiallyFulfilled, OrderStatus.Fulfilled, OrderStatus.Refunded };

    public static IEndpointRouteBuilder MapAdminReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/reports").WithTags("Admin: Reports").RequireAuthorization("Admin");
        group.MapGet("/sales", SalesOverTime);
        group.MapGet("/top-products", TopProducts);
        group.MapGet("/low-stock", LowStock);
        group.MapGet("/discounts", DiscountUsage);
        return app;
    }

    private static async Task<IResult> SalesOverTime(BazaarDbContext db, CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => SoldStatuses.Contains(o.Status))
            .Select(o => new { o.PlacedAt, o.GrandTotal.Amount, o.Currency })
            .ToListAsync(ct);

        var currency = rows.Count > 0 ? rows[0].Currency : Money.DefaultCurrency;
        var buckets = rows
            .GroupBy(r => r.PlacedAt.UtcDateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new SalesBucketDto(
                g.Key.ToString("yyyy-MM-dd"),
                g.Count(),
                new MoneyDto(g.Sum(x => x.Amount), currency)))
            .ToList();

        var totalRevenue = rows.Sum(r => r.Amount);
        return Results.Ok(new SalesReportDto(buckets, rows.Count, new MoneyDto(totalRevenue, currency)));
    }

    private static async Task<IResult> TopProducts(BazaarDbContext db, int? limit, CancellationToken ct)
    {
        var take = limit is > 0 and <= 100 ? limit.Value : 10;

        var lines = await db.Orders.AsNoTracking()
            .Where(o => SoldStatuses.Contains(o.Status))
            .SelectMany(o => o.Items)
            .Select(li => new { li.Sku, li.Title, li.Quantity, li.LineTotal.Amount, li.LineTotal.Currency })
            .ToListAsync(ct);

        var top = lines
            .GroupBy(l => l.Sku)
            .Select(g => new TopProductDto(
                g.Key,
                g.First().Title,
                g.Sum(x => x.Quantity),
                new MoneyDto(g.Sum(x => x.Amount), g.First().Currency)))
            .OrderByDescending(p => p.QuantitySold)
            .ThenByDescending(p => p.Revenue.Amount)
            .Take(take)
            .ToList();

        return Results.Ok(top);
    }

    private static async Task<IResult> LowStock(BazaarDbContext db, int? threshold, CancellationToken ct)
    {
        var limit = threshold ?? 10;

        var items = await (
            from i in db.InventoryItems.AsNoTracking()
            join v in db.Variants.AsNoTracking() on i.VariantId equals v.Id
            join p in db.Products.AsNoTracking() on v.ProductId equals p.Id
            where (i.OnHand - i.Reserved) <= limit
            orderby (i.OnHand - i.Reserved)
            select new LowStockDto(v.Id, v.Sku, p.Title, i.OnHand - i.Reserved))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> DiscountUsage(BazaarDbContext db, CancellationToken ct)
    {
        var discounts = await db.DiscountCodes.AsNoTracking()
            .OrderByDescending(d => d.TimesUsed).ThenBy(d => d.Code)
            .Select(d => new DiscountUsageDto(d.Code, d.Type.ToString(), d.TimesUsed, d.UsageLimit))
            .ToListAsync(ct);

        return Results.Ok(discounts);
    }
}
