using Bazaar.Domain;
using Bazaar.Domain.Carts;
using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;
using Bazaar.Domain.Inventory;
using Bazaar.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Tests.Persistence;

public class ConcurrencyTests
{
    [Fact]
    public void A_stale_stock_write_raises_a_concurrency_conflict()
    {
        using var testDb = new TestDb();

        Guid variantId;
        using (var seed = testDb.NewContext())
        {
            var product = new Product { Slug = "conc-stock", Title = "Conc", Status = ProductStatus.Active };
            var variant = new ProductVariant { Sku = "CONC-STOCK", Price = new Money(10m, "USD") };
            product.AddVariant(variant);
            seed.Products.Add(product);
            seed.InventoryItems.Add(new InventoryItem { VariantId = variant.Id, OnHand = 10 });
            seed.SaveChanges();
            variantId = variant.Id;
        }

        using var ctxA = testDb.NewContext();
        using var ctxB = testDb.NewContext();
        var a = ctxA.InventoryItems.Single(i => i.VariantId == variantId);
        var b = ctxB.InventoryItems.Single(i => i.VariantId == variantId);

        a.OnHand = 5;
        ctxA.SaveChanges(); // wins, refreshes the stamp

        b.OnHand = 3;
        Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());
    }

    [Fact]
    public void A_stale_cart_write_raises_a_concurrency_conflict()
    {
        using var testDb = new TestDb();

        Guid cartId;
        using (var seed = testDb.NewContext())
        {
            var cart = new Cart();
            seed.Carts.Add(cart);
            seed.SaveChanges();
            cartId = cart.Id;
        }

        using var ctxA = testDb.NewContext();
        using var ctxB = testDb.NewContext();
        var a = ctxA.Carts.Single(c => c.Id == cartId);
        var b = ctxB.Carts.Single(c => c.Id == cartId);

        a.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        ctxA.SaveChanges();

        b.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(2);
        Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());
    }
}
