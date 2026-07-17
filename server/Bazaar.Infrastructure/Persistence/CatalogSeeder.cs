using Bazaar.Domain;
using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;
using Bazaar.Domain.Discounts;
using Bazaar.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Persistence;

/// <summary>Seeds a small development catalog: collections, products, variants, stock and a discount code.</summary>
public static class CatalogSeeder
{
    public static async Task SeedAsync(BazaarDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct))
            return;

        var apparel = new Collection { Slug = "apparel", Title = "Apparel", Description = "Wearables for every season." };
        var accessories = new Collection { Slug = "accessories", Title = "Accessories", Description = "The finishing touches." };
        var homeGoods = new Collection { Slug = "home", Title = "Home Goods", Description = "For the well-appointed space." };
        db.Collections.AddRange(apparel, accessories, homeGoods);

        var inventory = new List<InventoryItem>();

        Product Build(
            string slug, string title, string description, string? vendor,
            string imageUrl, IEnumerable<Collection> collections,
            IEnumerable<(string sku, string variantTitle, decimal price, int stock, (string name, string value)[] options)> variants)
        {
            var product = new Product
            {
                Slug = slug,
                Title = title,
                Description = description,
                Vendor = vendor,
                Status = ProductStatus.Active,
            };
            product.AddImage(new ProductImage { Url = imageUrl, AltText = title, Position = 0 });

            var position = 0;
            foreach (var (sku, variantTitle, price, stock, options) in variants)
            {
                var variant = new ProductVariant
                {
                    Sku = sku,
                    Title = variantTitle,
                    Price = new Money(price, Money.DefaultCurrency),
                    Position = position++,
                };
                foreach (var (name, value) in options)
                    variant.SetOption(name, value);
                product.AddVariant(variant);
                inventory.Add(new InventoryItem { VariantId = variant.Id, OnHand = stock, Reserved = 0 });
            }

            foreach (var collection in collections)
                product.AddToCollection(collection);

            return product;
        }

        var products = new[]
        {
            Build("classic-tee", "Classic Cotton Tee", "A soft, breathable everyday t-shirt.", "Bazaar Basics",
                "https://images.bazaar.test/classic-tee.jpg", new[] { apparel },
                new[]
                {
                    ("TEE-S-BLK", "Small / Black", 19.99m, 40, new[] { ("Size", "Small"), ("Color", "Black") }),
                    ("TEE-M-BLK", "Medium / Black", 19.99m, 55, new[] { ("Size", "Medium"), ("Color", "Black") }),
                    ("TEE-L-WHT", "Large / White", 19.99m, 30, new[] { ("Size", "Large"), ("Color", "White") }),
                }),
            Build("merino-hoodie", "Merino Wool Hoodie", "Temperature-regulating merino, built to last.", "Northwind Apparel",
                "https://images.bazaar.test/merino-hoodie.jpg", new[] { apparel },
                new[]
                {
                    ("HOOD-M-GRY", "Medium / Grey", 89.00m, 20, new[] { ("Size", "Medium"), ("Color", "Grey") }),
                    ("HOOD-L-GRY", "Large / Grey", 89.00m, 18, new[] { ("Size", "Large"), ("Color", "Grey") }),
                }),
            Build("leather-belt", "Full-Grain Leather Belt", "Hand-finished full-grain leather with a brass buckle.", "Atlas Goods",
                "https://images.bazaar.test/leather-belt.jpg", new[] { accessories },
                new[]
                {
                    ("BELT-32", "32 inch", 45.50m, 25, new[] { ("Size", "32") }),
                    ("BELT-34", "34 inch", 45.50m, 22, new[] { ("Size", "34") }),
                }),
            Build("canvas-tote", "Canvas Market Tote", "A roomy, sturdy tote for the weekend market.", "Bazaar Basics",
                "https://images.bazaar.test/canvas-tote.jpg", new[] { accessories, homeGoods },
                new[]
                {
                    ("TOTE-NAT", "Natural", 24.00m, 60, new[] { ("Color", "Natural") }),
                    ("TOTE-OLV", "Olive", 24.00m, 35, new[] { ("Color", "Olive") }),
                }),
            Build("ceramic-mug", "Stoneware Coffee Mug", "A 12oz stoneware mug with a comfortable handle.", "Kiln & Co",
                "https://images.bazaar.test/ceramic-mug.jpg", new[] { homeGoods },
                new[]
                {
                    ("MUG-CRM", "Cream", 14.00m, 80, new[] { ("Color", "Cream") }),
                    ("MUG-SLT", "Slate", 14.00m, 12, new[] { ("Color", "Slate") }),
                }),
            Build("wool-blanket", "Chunky Wool Throw", "A cozy hand-loomed wool throw blanket.", "Northwind Apparel",
                "https://images.bazaar.test/wool-blanket.jpg", new[] { homeGoods },
                new[]
                {
                    ("BLNK-OAT", "Oatmeal", 120.00m, 8, new[] { ("Color", "Oatmeal") }),
                }),
        };

        db.Products.AddRange(products);
        db.InventoryItems.AddRange(inventory);

        db.DiscountCodes.Add(new DiscountCode
        {
            Code = "WELCOME10",
            Type = DiscountType.Percentage,
            Value = 10m,
            IsActive = true,
            UsageLimit = 1000,
        });
        db.DiscountCodes.Add(new DiscountCode
        {
            Code = "SHIP5",
            Type = DiscountType.FixedAmount,
            Value = 5m,
            Currency = Money.DefaultCurrency,
            IsActive = true,
        });

        await db.SaveChangesAsync(ct);
    }
}
