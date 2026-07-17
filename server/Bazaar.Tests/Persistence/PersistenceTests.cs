using Bazaar.Domain.Catalog;
using Bazaar.Infrastructure.Persistence;
using Bazaar.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Tests.Persistence;

public class PersistenceTests
{
    [Fact]
    public async Task Seed_populates_the_development_catalog()
    {
        using var db = new TestDb();
        await using var ctx = db.NewContext();

        await CatalogSeeder.SeedAsync(ctx);

        Assert.Equal(6, await ctx.Products.CountAsync());
        Assert.Equal(3, await ctx.Collections.CountAsync());
        Assert.Equal(12, await ctx.Variants.CountAsync());
        Assert.Equal(12, await ctx.InventoryItems.CountAsync());
        Assert.Equal(2, await ctx.DiscountCodes.CountAsync());
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        using var db = new TestDb();
        await using var ctx = db.NewContext();

        await CatalogSeeder.SeedAsync(ctx);
        await CatalogSeeder.SeedAsync(ctx);

        Assert.Equal(6, await ctx.Products.CountAsync());
    }

    [Fact]
    public async Task Variant_price_round_trips_as_money()
    {
        using var db = new TestDb();
        await using var ctx = db.NewContext();
        await CatalogSeeder.SeedAsync(ctx);

        var variant = await ctx.Variants.SingleAsync(v => v.Sku == "TEE-S-BLK");

        Assert.Equal(19.99m, variant.Price.Amount);
        Assert.Equal("USD", variant.Price.Currency);
        Assert.Equal(1999, variant.Price.ToCents());
    }

    [Fact]
    public async Task DateTimeOffset_columns_support_ordering_and_filtering_in_sql()
    {
        using var db = new TestDb();
        await using var ctx = db.NewContext();
        await CatalogSeeder.SeedAsync(ctx);

        // These translate to SQL against the UTC-ticks (long) column; without the converter
        // SQLite would order/compare DateTimeOffset text incorrectly.
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-1);

        var ordered = await ctx.Products
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.Slug)
            .ToListAsync();
        var recent = await ctx.Products
            .Where(p => p.CreatedAt >= cutoff && p.CreatedAt <= now)
            .CountAsync();

        Assert.Equal(6, ordered.Count);
        Assert.Equal(6, recent);
    }

    [Fact]
    public async Task Product_graph_persists_variants_images_and_collections()
    {
        using var db = new TestDb();
        await using var ctx = db.NewContext();
        await CatalogSeeder.SeedAsync(ctx);

        var product = await ctx.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Include(p => p.Collections)
            .SingleAsync(p => p.Slug == "canvas-tote");

        Assert.Equal(2, product.Variants.Count);
        Assert.Single(product.Images);
        Assert.Equal(2, product.Collections.Count); // accessories + home
    }

    [Fact]
    public async Task Reserving_stock_persists_and_reduces_availability()
    {
        using var db = new TestDb();
        await using (var seedCtx = db.NewContext())
            await CatalogSeeder.SeedAsync(seedCtx);

        Guid variantId;
        await using (var writeCtx = db.NewContext())
        {
            var variant = await writeCtx.Variants.SingleAsync(v => v.Sku == "BLNK-OAT");
            variantId = variant.Id;
            var item = await writeCtx.InventoryItems.SingleAsync(i => i.VariantId == variantId);
            Assert.Equal(8, item.OnHand);
            item.Reserve(3);
            await writeCtx.SaveChangesAsync();
        }

        await using var readCtx = db.NewContext();
        var reloaded = await readCtx.InventoryItems.SingleAsync(i => i.VariantId == variantId);
        Assert.Equal(3, reloaded.Reserved);
        Assert.Equal(5, reloaded.Available);
    }
}
